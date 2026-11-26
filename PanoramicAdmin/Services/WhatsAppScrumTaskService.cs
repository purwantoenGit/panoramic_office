using Microsoft.EntityFrameworkCore;
using PanoramicAdmin.Models;

namespace PanoramicAdmin.Services;

public sealed class WhatsAppScrumTaskService(IDbContextFactory<AppDbContext> dbContextFactory)
{
    private static readonly string[] ActiveTaskStatuses = ["Belum dimulai", "Sedang dikerjakan"];

    public async Task<WhatsAppScrumTaskLookup> GetMyTasksAsync(
        string? phoneNumber,
        string? whatsAppId,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var matchingPics = await db.MasterPics.AsNoTracking()
            .Where(pic =>
                (phoneNumber != null && pic.PhoneNumber == phoneNumber) ||
                (whatsAppId != null && pic.WhatsAppId == whatsAppId))
            .Select(pic => new { pic.Nama })
            .Take(2)
            .ToListAsync(cancellationToken);

        if (matchingPics.Count == 0)
        {
            return new WhatsAppScrumTaskLookup(false, false, null, []);
        }

        if (matchingPics.Count > 1)
        {
            return new WhatsAppScrumTaskLookup(false, true, null, []);
        }

        var picName = matchingPics[0].Nama;
        var sprints = await db.ScrumSprints.AsNoTracking()
            .Where(sprint => sprint.Initiative == ScrumInitiative.SmartRefueling)
            .OrderByDescending(sprint => sprint.Status == "Berjalan")
            .ThenByDescending(sprint => sprint.StartDate)
            .ThenByDescending(sprint => sprint.Id)
            .Select(sprint => new
            {
                sprint.Id,
                sprint.Focus,
                sprint.StartDate,
                sprint.EndDate,
                sprint.Status,
                sprint.ProductBacklogItemId,
                ProductBacklogItemNumber = sprint.ProductBacklogItem == null
                    ? (int?)null
                    : sprint.ProductBacklogItem.InitiativeNumber
            })
            .ToListAsync(cancellationToken);

        var pbiSprints = sprints
            .Where(sprint =>
                sprint.ProductBacklogItemId.HasValue &&
                sprint.ProductBacklogItemNumber.HasValue)
            .ToList();
        if (pbiSprints.Count == 0)
        {
            return new WhatsAppScrumTaskLookup(true, false, picName, []);
        }

        var sprintIds = pbiSprints.Select(sprint => sprint.Id).ToArray();
        var scrumTasks = await db.ScrumTasks.AsNoTracking()
            .Where(task => sprintIds.Contains(task.SprintId) &&
                task.Sprint!.Initiative == ScrumInitiative.SmartRefueling)
            .OrderBy(task => task.Id)
            .Select(task => new
            {
                task.Id,
                task.SprintId,
                ProductBacklogItemNumber = task.Sprint!.ProductBacklogItem == null
                    ? (int?)null
                    : task.Sprint.ProductBacklogItem.InitiativeNumber,
                task.Title,
                task.Owner,
                task.Priority,
                task.Status,
                task.EstimatedHour
            })
            .ToListAsync(cancellationToken);

        var taskNumbersById = scrumTasks
            .Where(task => task.ProductBacklogItemNumber.HasValue)
            .GroupBy(task => task.ProductBacklogItemNumber!.Value)
            .SelectMany(group => group.OrderBy(task => task.Id)
                .Select((task, index) => new
                {
                    task.Id,
                    Number = group.Key * 100 + index + 1
                }))
            .ToDictionary(task => task.Id, task => task.Number);

        var tasksBySprintId = scrumTasks
            .Where(task =>
                string.Equals(task.Owner, picName, StringComparison.OrdinalIgnoreCase) &&
                ActiveTaskStatuses.Contains(task.Status, StringComparer.Ordinal))
            .GroupBy(task => task.SprintId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var sprintTaskGroups = pbiSprints
            .Where(sprint => tasksBySprintId.ContainsKey(sprint.Id))
            .Select(sprint => new WhatsAppScrumSprintTasks(
                ScrumInitiative.GetProductBacklogItemCode(
                    ScrumInitiative.SmartRefueling, sprint.ProductBacklogItemNumber!.Value),
                sprint.Focus,
                sprint.StartDate,
                sprint.EndDate,
                sprint.Status,
                tasksBySprintId[sprint.Id]
                    .OrderBy(task => task.Status == "Sedang dikerjakan" ? 0 : 1)
                    .ThenBy(task => task.Id)
                    .Select(task => new WhatsAppScrumTaskItem(
                        task.ProductBacklogItemNumber.HasValue
                            ? ScrumInitiative.GetTaskCode(
                                ScrumInitiative.SmartRefueling, taskNumbersById[task.Id])
                            : ScrumInitiative.GetTaskCode(ScrumInitiative.SmartRefueling, task.Id),
                        task.Title,
                        task.Status,
                        task.Priority,
                        task.EstimatedHour))
                    .ToList()))
            .ToList();

        return new WhatsAppScrumTaskLookup(
            true,
            false,
            picName,
            sprintTaskGroups);
    }
}

public sealed record WhatsAppScrumTaskRequest(string? PhoneNumber, string? WhatsAppId);

public sealed record WhatsAppScrumTaskLookup(
    bool PicFound,
    bool IdentityAmbiguous,
    string? PicName,
    IReadOnlyList<WhatsAppScrumSprintTasks> Sprints);

public sealed record WhatsAppScrumSprintTasks(
    string ProductBacklogItemCode,
    string SprintFocus,
    DateTime StartDate,
    DateTime EndDate,
    string Status,
    IReadOnlyList<WhatsAppScrumTaskItem> Tasks);

public sealed record WhatsAppScrumTaskItem(
    string Code,
    string Title,
    string Status,
    string Priority,
    decimal? EstimatedHour);
