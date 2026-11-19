using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using PanoramicAdmin.Models;

namespace PanoramicAdmin.Services;

public sealed class PitStopMappingService(RfidEquipmentService equipmentService)
{
    public async Task<IReadOnlyList<PitStopWarehouseStatus>> GetPitStopStatusesAsync(
        int days,
        CancellationToken cancellationToken = default)
        => await GetWarehouseStatusesAsync("FP", days, cancellationToken);

    public async Task<IReadOnlyList<PitStopWarehouseStatus>> GetFuelTruckStatusesAsync(
        int days,
        CancellationToken cancellationToken = default)
        => await GetWarehouseStatusesAsync("FT", days, cancellationToken);

    public async Task<FuelTruckDistrictSummary> GetFuelTruckDistrictSummaryAsync(
        string district,
        int days,
        CancellationToken cancellationToken = default)
    {
        ValidateDays(days);
        ArgumentException.ThrowIfNullOrWhiteSpace(district);

        var normalizedDistrict = district.Trim().ToUpperInvariant();
        var statuses = await GetFuelTruckStatusesAsync(days, cancellationToken);
        var districtStatuses = statuses
            .Where(status => string.Equals(
                status.District, normalizedDistrict, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var rfidWarehouseIds = districtStatuses
            .Where(status => status.HasRfid)
            .Select(status => status.WarehouseId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(warehouseId => warehouseId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var manualWarehouseIds = districtStatuses
            .Where(status => !status.HasRfid && status.HasNonRfid)
            .Select(status => status.WarehouseId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(warehouseId => warehouseId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var totalWarehouseCount = rfidWarehouseIds.Length + manualWarehouseIds.Length;
        var percentage = totalWarehouseCount == 0
            ? 0
            : decimal.Round((decimal)rfidWarehouseIds.Length / totalWarehouseCount * 100, 2);

        return new FuelTruckDistrictSummary(
            normalizedDistrict,
            days,
            rfidWarehouseIds.Length,
            totalWarehouseCount,
            percentage,
            rfidWarehouseIds,
            manualWarehouseIds);
    }

    private async Task<IReadOnlyList<PitStopWarehouseStatus>> GetWarehouseStatusesAsync(
        string warehousePrefix,
        int days,
        CancellationToken cancellationToken)
    {
        ValidateDays(days);

        await using var connection = equipmentService.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [dstrct_code])))) AS [District],
                UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [whouse_id])))) AS [WarehouseId],
                MAX(CASE
                    WHEN UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [input_type])))) = N'RFID'
                    THEN 1 ELSE 0
                END) AS [HasRfid],
                MAX(CASE
                    WHEN [input_type] IS NOT NULL
                     AND UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [input_type])))) <> N'RFID'
                    THEN 1 ELSE 0
                END) AS [HasNonRfid]
            FROM [DB_SATUPAMA_SM].[dbo].[tbl_t_log_sheet_detail]
            WHERE [whouse_id] LIKE @WarehousePattern
              AND [issued_date] >= DATEADD(DAY, -@Days, CONVERT(date, GETDATE()))
              AND [issued_date] < DATEADD(DAY, 1, CONVERT(date, GETDATE()))
            GROUP BY
                UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [dstrct_code])))),
                UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [whouse_id]))))
            ORDER BY [District], [WarehouseId];
            """;
        command.Parameters.Add("@Days", SqlDbType.Int).Value = days;
        command.Parameters.Add("@WarehousePrefix", SqlDbType.NVarChar, 2).Value = warehousePrefix;
        command.Parameters.Add("@WarehousePattern", SqlDbType.NVarChar, 3).Value = $"{warehousePrefix}%";

        var statuses = new List<PitStopWarehouseStatus>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            statuses.Add(new PitStopWarehouseStatus(
                ReadText(reader, 0),
                ReadText(reader, 1),
                reader.GetInt32(2) == 1,
                reader.GetInt32(3) == 1));
        }

        return statuses;
    }

    public async Task<IReadOnlyList<FuelTruckRfidDailyAchievement>> GetFuelTruckRfidAchievementsAsync(
        string district,
        string warehouseId,
        int days,
        CancellationToken cancellationToken = default)
    {
        ValidateDays(days);
        ArgumentException.ThrowIfNullOrWhiteSpace(district);
        ArgumentException.ThrowIfNullOrWhiteSpace(warehouseId);

        var normalizedWarehouseId = warehouseId.Trim().ToUpperInvariant();
        if (!normalizedWarehouseId.StartsWith("FT", StringComparison.Ordinal))
        {
            throw new ArgumentException("Kode warehouse harus merupakan Fuel Truck.", nameof(warehouseId));
        }

        await using var connection = equipmentService.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            ;WITH [DateRange] AS
            (
                SELECT CONVERT(date, DATEADD(DAY, -@Days, GETDATE())) AS [AchievementDate]
                UNION ALL
                SELECT DATEADD(DAY, 1, [AchievementDate])
                FROM [DateRange]
                WHERE [AchievementDate] < CONVERT(date, GETDATE())
            ),
            [DailyRfid] AS
            (
                SELECT
                    CONVERT(date, [issued_date]) AS [AchievementDate],
                    COUNT_BIG(CASE
                        WHEN [input_type] IS NOT NULL
                         AND UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [input_type])))) <> N'RFID'
                        THEN 1
                    END) AS [ManualTransactionCount],
                    COUNT_BIG(CASE
                        WHEN UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [input_type])))) = N'RFID'
                        THEN 1
                    END) AS [RfidCount]
                FROM [DB_SATUPAMA_SM].[dbo].[tbl_t_log_sheet_detail]
                WHERE UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [dstrct_code])))) = @District
                  AND UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [whouse_id])))) = @WarehouseId
                  AND [issued_date] >= DATEADD(DAY, -@Days, CONVERT(date, GETDATE()))
                  AND [issued_date] < DATEADD(DAY, 1, CONVERT(date, GETDATE()))
                GROUP BY CONVERT(date, [issued_date])
            )
            SELECT
                [DateRange].[AchievementDate],
                ISNULL([DailyRfid].[ManualTransactionCount], CONVERT(bigint, 0)) AS [ManualTransactionCount],
                ISNULL([DailyRfid].[RfidCount], CONVERT(bigint, 0)) AS [RfidCount]
            FROM [DateRange]
            LEFT JOIN [DailyRfid]
                ON [DailyRfid].[AchievementDate] = [DateRange].[AchievementDate]
            ORDER BY [DateRange].[AchievementDate]
            OPTION (MAXRECURSION 100);
            """;
        command.Parameters.Add("@Days", SqlDbType.Int).Value = days;
        command.Parameters.Add("@District", SqlDbType.NVarChar, 50).Value = district.Trim().ToUpperInvariant();
        command.Parameters.Add("@WarehouseId", SqlDbType.NVarChar, 50).Value = normalizedWarehouseId;

        var achievements = new List<FuelTruckRfidDailyAchievement>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            achievements.Add(new FuelTruckRfidDailyAchievement(
                reader.GetDateTime(0),
                reader.GetInt64(1),
                reader.GetInt64(2)));
        }

        return achievements;
    }

    public async Task<IReadOnlyList<string>> GetFuelTruckUnitNumbersAsync(
        string district,
        string warehouseId,
        DateTime achievementDate,
        bool isRfid,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(district);
        ArgumentException.ThrowIfNullOrWhiteSpace(warehouseId);

        var normalizedWarehouseId = warehouseId.Trim().ToUpperInvariant();
        if (!normalizedWarehouseId.StartsWith("FT", StringComparison.Ordinal))
        {
            throw new ArgumentException("Kode warehouse harus merupakan Fuel Truck.", nameof(warehouseId));
        }

        await using var connection = equipmentService.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DISTINCT
                UPPER(LTRIM(RTRIM(CONVERT(nvarchar(4000), [unit_no])))) AS [UnitNo]
            FROM [DB_SATUPAMA_SM].[dbo].[tbl_t_log_sheet_detail]
            WHERE UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [dstrct_code])))) = @District
              AND UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [whouse_id])))) = @WarehouseId
              AND
              (
                  (@IsRfid = 1 AND UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [input_type])))) = N'RFID')
                  OR
                  (@IsRfid = 0 AND [input_type] IS NOT NULL
                   AND UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [input_type])))) <> N'RFID')
              )
              AND [issued_date] >= @AchievementDate
              AND [issued_date] < DATEADD(DAY, 1, @AchievementDate)
              AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(4000), [unit_no]))), N'') IS NOT NULL
            ORDER BY [UnitNo];
            """;
        command.Parameters.Add("@District", SqlDbType.NVarChar, 50).Value = district.Trim().ToUpperInvariant();
        command.Parameters.Add("@WarehouseId", SqlDbType.NVarChar, 50).Value = normalizedWarehouseId;
        command.Parameters.Add("@AchievementDate", SqlDbType.Date).Value = achievementDate.Date;
        command.Parameters.Add("@IsRfid", SqlDbType.Bit).Value = isRfid;

        var unitNumbers = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            unitNumbers.Add(ReadText(reader, 0));
        }

        return unitNumbers;
    }

    public async Task<IReadOnlyList<string>> GetFallbackDistrictsAsync(
        string warehousePrefix,
        CancellationToken cancellationToken = default)
    {
        if (warehousePrefix is not ("FP" or "FT"))
        {
            throw new ArgumentOutOfRangeException(nameof(warehousePrefix));
        }

        await using var connection = equipmentService.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DISTINCT UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [dstrct_code])))) AS [District]
            FROM [DB_SATUPAMA_SM].[dbo].[tbl_t_log_sheet_detail]
            WHERE
                (@WarehousePrefix = N'FP' AND [dstrct_code] NOT IN
                 (N'ABKL', N'BAYA', N'BBSO', N'ERKA', N'KPCT', N'NPRL'))
                OR
                (@WarehousePrefix = N'FT' AND [dstrct_code] NOT IN
                 (N'ABKL', N'ARIA', N'BBSO', N'BEKB', N'BPOP',
                  N'BRCS', N'BTSJ', N'ERKA', N'HMNT', N'KPCT', N'NPRL'))
            ORDER BY [District];
            """;
        command.Parameters.Add("@WarehousePrefix", SqlDbType.NVarChar, 2).Value = warehousePrefix;

        var districts = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            districts.Add(ReadText(reader, 0));
        }

        return districts;
    }

    private static void ValidateDays(int days)
    {
        if (days is < 1 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(days), "Periode harus antara 1 dan 90 hari.");
        }
    }

    private static string ReadText(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
            ? string.Empty
            : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
}
