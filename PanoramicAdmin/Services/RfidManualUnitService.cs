using Microsoft.EntityFrameworkCore;
using PanoramicAdmin.Models;

namespace PanoramicAdmin.Services;

public sealed class RfidManualUnitService(IDbContextFactory<AppDbContext> dbContextFactory)
{
    public async Task<IReadOnlyDictionary<RfidManualUnitKey, int>> GetCountsAsync()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var records = await db.RfidTagManualUnits.AsNoTracking().ToListAsync();

        return records.ToDictionary(
            record => new RfidManualUnitKey(record.District, record.EquipClass, record.Egi),
            record => record.AdditionalUnitCount);
    }

    public async Task SaveAsync(RfidManualUnitKey key, int? additionalUnitCount)
    {
        if (additionalUnitCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(additionalUnitCount),
                "Jumlah tambahan tidak boleh negatif.");
        }

        await using var db = await dbContextFactory.CreateDbContextAsync();
        if (key.EquipClass == "DT" && key.Egi == "A60H/A60J")
        {
            var combinedAndLegacyRecords = await db.RfidTagManualUnits
                .Where(item =>
                    item.District == key.District &&
                    item.EquipClass == key.EquipClass &&
                    (item.Egi == key.Egi || item.Egi == "A60H" || item.Egi == "A60J"))
                .ToListAsync();
            var combinedRecord = combinedAndLegacyRecords
                .SingleOrDefault(item => item.Egi == key.Egi);

            if (additionalUnitCount is null)
            {
                db.RfidTagManualUnits.RemoveRange(combinedAndLegacyRecords);
            }
            else
            {
                if (combinedRecord is null)
                {
                    db.RfidTagManualUnits.Add(new RfidTagManualUnit
                    {
                        District = key.District,
                        EquipClass = key.EquipClass,
                        Egi = key.Egi,
                        AdditionalUnitCount = additionalUnitCount.Value
                    });
                }
                else
                {
                    combinedRecord.AdditionalUnitCount = additionalUnitCount.Value;
                }

                db.RfidTagManualUnits.RemoveRange(combinedAndLegacyRecords
                    .Where(item => item.Egi == "A60H" || item.Egi == "A60J"));
            }

            await db.SaveChangesAsync();
            return;
        }

        var record = await db.RfidTagManualUnits.SingleOrDefaultAsync(item =>
            item.District == key.District &&
            item.EquipClass == key.EquipClass &&
            item.Egi == key.Egi);

        if (additionalUnitCount is null)
        {
            if (record is not null)
            {
                db.RfidTagManualUnits.Remove(record);
            }
        }
        else if (record is null)
        {
            db.RfidTagManualUnits.Add(new RfidTagManualUnit
            {
                District = key.District,
                EquipClass = key.EquipClass,
                Egi = key.Egi,
                AdditionalUnitCount = additionalUnitCount.Value
            });
        }
        else
        {
            record.AdditionalUnitCount = additionalUnitCount.Value;
        }

        await db.SaveChangesAsync();
    }
}
