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

    private async Task<IReadOnlyList<PitStopWarehouseStatus>> GetWarehouseStatusesAsync(
        string warehousePrefix,
        int days,
        CancellationToken cancellationToken)
    {
        if (days is < 1 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(days), "Periode harus antara 1 dan 90 hari.");
        }

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

    private static string ReadText(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
            ? string.Empty
            : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
}
