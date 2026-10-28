using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using PanoramicAdmin.Models;

namespace PanoramicAdmin.Services;

public sealed class RfidEquipmentService(
    IConfiguration configuration,
    IHostEnvironment environment)
{
    public async Task<IReadOnlyList<RfidEquipmentGroup>> SearchAsync(
        string? searchText,
        CancellationToken cancellationToken = default)
    {
        var hasSearch = !string.IsNullOrWhiteSpace(searchText);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            WITH EquipmentRows AS
            (
                SELECT
                    CASE WHEN [district] IS NULL
                         THEN N'(Kosong)'
                         ELSE CONVERT(nvarchar(4000), [district])
                    END AS [District],
                    UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [equip_class])))) AS [EquipClass],
                    CASE
                        WHEN UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [equip_class])))) = N'DT'
                         AND UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [egi]))))
                             IN (N'A60H', N'A60J')
                            THEN N'A60H/A60J'
                        WHEN [egi] IS NULL THEN N'(Kosong)'
                        ELSE CONVERT(nvarchar(4000), [egi])
                    END AS [Egi],
                    CONVERT(nvarchar(4000), [unit_no]) AS [UnitNo]
                FROM [dbo].[tbl_r_equip]
                WHERE [STATUS] = 1
                  AND UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [equip_class]))))
                      IN (N'DT', N'EX', N'GR', N'DZ')
                  AND
                  (
                      UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [equip_class])))) <> N'DT'
                      OR UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [egi]))))
                          IN (N'A40G', N'A60H', N'A60J', N'HD7857', N'HM4002', N'HM4003', N'HM4003R')
                  )
                  AND
                  (
                      UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [equip_class])))) <> N'EX'
                      OR UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [egi]))))
                          IN
                          (
                              N'PC125011R', N'PC1250SP8', N'PC200011R', N'PC20008',
                              N'PC2007', N'PC2008', N'PC3008', N'PC3400EX11',
                              N'PC400SE8', N'PC8508R1'
                          )
                  )
                  AND
                  (
                      UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [equip_class])))) <> N'GR'
                      OR UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [egi])))) = N'GD825A2'
                  )
                  AND
                  (
                      UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [equip_class])))) <> N'DZ'
                      OR UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [egi]))))
                          IN (N'D155A6R', N'D375A6R')
                  )
                  AND
                  (
                      @hasSearch = 0
                      OR CONVERT(nvarchar(4000), [unit_no]) LIKE @searchPattern
                      OR CONVERT(nvarchar(4000), [district]) LIKE @searchPattern
                      OR CONVERT(nvarchar(4000), [egi]) LIKE @searchPattern
                      OR
                      (
                          UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [equip_class])))) = N'DT'
                          AND UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), [egi]))))
                              IN (N'A60H', N'A60J')
                          AND N'A60H/A60J' LIKE @searchPattern
                      )
                      OR CONVERT(nvarchar(4000), [equip_class]) LIKE @searchPattern
                  )
            )
            SELECT [District], [EquipClass], [Egi],
                   COUNT(DISTINCT [UnitNo]) AS [UnitCount]
            FROM EquipmentRows
            GROUP BY [District], [EquipClass], [Egi]
            ORDER BY [EquipClass], [District], [Egi];
            """;
        command.Parameters.Add("@hasSearch", SqlDbType.Bit).Value = hasSearch;
        command.Parameters.Add("@searchPattern", SqlDbType.NVarChar, 4000).Value =
            hasSearch ? $"%{searchText!.Trim()}%" : string.Empty;

        var equipment = new List<RfidEquipmentGroup>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            equipment.Add(new RfidEquipmentGroup(
                ReadText(reader, 0),
                ReadText(reader, 1),
                ReadText(reader, 2),
                reader.GetInt32(3)));
        }

        return equipment;
    }

    public async Task<IReadOnlyDictionary<RfidManualUnitKey, int>> GetRfidInputCountsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            WITH EquipmentRows AS
            (
                SELECT
                    CASE WHEN e.[district] IS NULL
                         THEN N'(Kosong)'
                         ELSE LTRIM(RTRIM(CONVERT(nvarchar(4000), e.[district])))
                    END AS [District],
                    UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), e.[equip_class])))) AS [EquipClass],
                    CASE
                        WHEN UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), e.[equip_class])))) = N'DT'
                         AND UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), e.[egi]))))
                             IN (N'A60H', N'A60J')
                            THEN N'A60H/A60J'
                        ELSE UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), e.[egi]))))
                    END AS [Egi],
                    UPPER(LTRIM(RTRIM(CONVERT(nvarchar(4000), e.[unit_no])))) AS [UnitNo],
                    LOWER(LTRIM(RTRIM(CONVERT(nvarchar(10), e.[STATUS])))) AS [Status]
                FROM [dbo].[tbl_r_equip] e
            ),
            ActiveUnits AS
            (
                SELECT DISTINCT [District], [EquipClass], [Egi], [UnitNo]
                FROM EquipmentRows
                WHERE [Status] IN (N'1', N'true')
                  AND NULLIF([UnitNo], N'') IS NOT NULL
                  AND
                  (
                      ([EquipClass] = N'DT' AND [Egi] IN
                       (
                           N'A40G', N'A60H/A60J', N'HD7857',
                           N'HM4002', N'HM4003', N'HM4003R'
                       ))
                      OR
                      ([EquipClass] = N'EX' AND [Egi] IN
                       (
                           N'PC125011R', N'PC1250SP8', N'PC200011R', N'PC20008',
                           N'PC2007', N'PC2008', N'PC3008', N'PC3400EX11',
                           N'PC400SE8', N'PC8508R1'
                       ))
                      OR ([EquipClass] = N'GR' AND [Egi] = N'GD825A2')
                      OR ([EquipClass] = N'DZ' AND [Egi] IN (N'D155A6R', N'D375A6R'))
                  )
            ),
            RfidLoggedUnits AS
            (
                SELECT DISTINCT
                    UPPER(LTRIM(RTRIM(CONVERT(nvarchar(4000), d.[unit_no])))) AS [UnitNo]
                FROM [dbo].[tbl_t_log_sheet_detail] d
                WHERE d.[issued_date] >= DATEADD(DAY, -30, GETDATE())
                  AND d.[issued_date] <= GETDATE()
                  AND UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), d.[input_type])))) = N'RFID'
                  AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(4000), d.[unit_no]))), N'') IS NOT NULL
            ),
            RfidActiveUnits AS
            (
                SELECT a.[District], a.[EquipClass], a.[Egi], a.[UnitNo]
                FROM ActiveUnits a
                INNER JOIN RfidLoggedUnits r
                    ON a.[UnitNo] COLLATE DATABASE_DEFAULT =
                       r.[UnitNo] COLLATE DATABASE_DEFAULT
            )
            SELECT
                [District],
                [EquipClass],
                [Egi],
                COUNT(DISTINCT [UnitNo]) AS [RfidUnitCount]
            FROM RfidActiveUnits
            GROUP BY [District], [EquipClass], [Egi]
            ORDER BY [District], [EquipClass], [Egi];
            """;

        var counts = new Dictionary<RfidManualUnitKey, int>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var key = new RfidManualUnitKey(
                ReadText(reader, 0),
                ReadText(reader, 1),
                ReadText(reader, 2));
            counts[key] = reader.GetInt32(3);
        }

        return counts;
    }

    private SqlConnection CreateConnection()
    {
        var connectionString = configuration.GetConnectionString("RfidDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string RfidDb belum dikonfigurasi.");
        }

        var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
        if (environment.IsDevelopment())
        {
            connectionStringBuilder.TrustServerCertificate = true;
        }

        return new SqlConnection(connectionStringBuilder.ConnectionString);
    }

    private static string ReadText(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
            ? string.Empty
            : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? string.Empty;
}
