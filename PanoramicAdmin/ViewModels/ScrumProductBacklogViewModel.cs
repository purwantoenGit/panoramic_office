using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using PanoramicAdmin.Models;
using PanoramicAdmin.Services;
using System.Data;

namespace PanoramicAdmin.ViewModels;

public sealed class ScrumProductBacklogViewModel(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IConfiguration configuration,
    ISnackbar snackbar,
    ILogger<ScrumProductBacklogViewModel> logger,
    ScrumProductBacklogImageStorage imageStorage)
{
    private const int MaximumImagesPerItem = ScrumProductBacklogImageValidator.MaximumImagesPerItem;

    public event Action? StateChanged;
    public IReadOnlyList<ScrumProductBacklogItemRow> Items { get; private set; } = [];
    public string Initiative { get; private set; } = ScrumInitiative.SmartRefueling;
    public bool IsLoading { get; private set; } = true;
    public bool IsSaving { get; private set; }
    public string? DatabaseError { get; private set; }
    public int TargetedItemCount => Items.Count(item => item.TargetSprint.HasValue);
    public int ItemsWithSprintCount => Items.Count(item => item.SprintCompletionPercent.HasValue);
    public int HighPriorityItemCount => Items.Count(item => item.Priority == "Tinggi");
    public double AverageSprintCompletionPercent =>
        Items.Where(item => item.SprintCompletionPercent.HasValue)
            .Select(item => item.SprintCompletionPercent!.Value)
            .DefaultIfEmpty()
            .Average();

    public async Task LoadAsync(string initiative = ScrumInitiative.SmartRefueling)
    {
        IsLoading = true;
        DatabaseError = null;
        NotifyStateChanged();
        try
        {
            if (!ScrumInitiative.IsSupported(initiative))
            {
                DatabaseError = "Inisiatif Product Backlog Item tidak valid.";
                logger.LogWarning("Unsupported Scrum initiative {Initiative}.", initiative);
                return;
            }

            Initiative = initiative;
            if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("PanoramicDb")))
            {
                DatabaseError = "Konfigurasi koneksi database PanoramicDb belum tersedia.";
                logger.LogError("The PanoramicDb connection string is missing or empty.");
                return;
            }

            await using var db = await dbContextFactory.CreateDbContextAsync();
            var sprintProgressByPbi = (await db.ScrumSprints.AsNoTracking()
                    .Where(sprint => sprint.Initiative == Initiative && sprint.ProductBacklogItemId.HasValue)
                    .Select(sprint => new
                    {
                        ProductBacklogItemId = sprint.ProductBacklogItemId.GetValueOrDefault(),
                        sprint.Id,
                        sprint.Status,
                        sprint.StartDate,
                        TaskCount = sprint.Tasks.Count(),
                        CompletedTaskCount = sprint.Tasks.Count(task => task.Status == "Selesai")
                    })
                    .ToListAsync())
                .GroupBy(sprint => sprint.ProductBacklogItemId)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var sprint = group
                            .OrderByDescending(item => item.Status == "Berjalan")
                            .ThenByDescending(item => item.StartDate)
                            .ThenByDescending(item => item.Id)
                            .First();
                        return sprint.TaskCount == 0
                            ? 0
                            : (double)sprint.CompletedTaskCount / sprint.TaskCount * 100;
                    });
            var items = await db.ScrumProductBacklogItems.AsNoTracking()
                    .Where(item => item.Initiative == Initiative)
                    .OrderBy(item => item.Id)
                    .Select(item => new ScrumProductBacklogItemRow(
                        item.Id,
                        ScrumInitiative.GetProductBacklogItemCode(Initiative, item.InitiativeNumber),
                        item.Module,
                        item.Summary,
                        item.AcceptanceCriteria,
                        item.Priority,
                        item.TargetSprint,
                        item.DefinitionOfDone,
                        null,
                        Array.Empty<ScrumProductBacklogItemImageRow>()))
                    .ToListAsync();
            var imagesByPbi = (await db.ScrumProductBacklogItemImages.AsNoTracking()
                    .Where(image => image.ProductBacklogItem!.Initiative == Initiative)
                    .OrderBy(image => image.Id)
                    .Select(image => new
                    {
                        image.Id,
                        image.ProductBacklogItemId,
                        image.FileName
                    })
                    .ToListAsync())
                .GroupBy(image => image.ProductBacklogItemId)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<ScrumProductBacklogItemImageRow>)group
                        .Select(image => new ScrumProductBacklogItemImageRow(
                            image.Id,
                            $"/uploads/pbi/{group.Key}/{image.FileName}"))
                        .ToList());
            Items = items.Select(item => item with
            {
                SprintCompletionPercent = sprintProgressByPbi.TryGetValue(item.Id, out var percentage)
                    ? percentage
                    : null,
                Images = imagesByPbi.GetValueOrDefault(item.Id, [])
            }).ToList();
        }
        catch (Exception exception)
        {
            HandleError(exception, "memuat Product Backlog Item",
                "Data Product Backlog Item gagal dimuat.");
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    public async Task SaveAsync(
        ScrumProductBacklogItemInput input,
        string initiative = ScrumInitiative.SmartRefueling)
    {
        if (!ScrumInitiative.IsSupported(initiative))
        {
            snackbar.Add("Inisiatif Product Backlog Item tidak valid.", Severity.Warning);
            return;
        }

        var module = input.Module.Trim();
        var summary = input.Summary.Trim();
        var acceptanceCriteria = input.AcceptanceCriteria.Trim();
        var definitionOfDone = input.DefinitionOfDone.Trim();
        var imageFiles = input.Images ?? [];
        if (string.IsNullOrWhiteSpace(module) || module.Length > 100 ||
            string.IsNullOrWhiteSpace(summary) || summary.Length > 250 ||
            string.IsNullOrWhiteSpace(acceptanceCriteria) ||
            string.IsNullOrWhiteSpace(definitionOfDone) ||
            !ScrumProductBacklogCatalog.Priorities.Contains(input.Priority, StringComparer.Ordinal) ||
            input.TargetSprint is < 1 ||
            imageFiles.Count > MaximumImagesPerItem)
        {
            snackbar.Add(
                $"Periksa data PBI. Maksimal {MaximumImagesPerItem} gambar dapat dipilih dalam sekali simpan.",
                Severity.Warning);
            return;
        }

        try
        {
            foreach (var image in imageFiles)
            {
                ScrumProductBacklogImageValidator.Validate(image);
            }
        }
        catch (InvalidDataException exception)
        {
            snackbar.Add(exception.Message, Severity.Warning);
            return;
        }
        IsSaving = true;
        try
        {
            await using var strategyDb = await dbContextFactory.CreateDbContextAsync();
            var executionStrategy = strategyDb.Database.CreateExecutionStrategy();
            var itemId = await executionStrategy.ExecuteAsync(async () =>
            {
                await using var db = await dbContextFactory.CreateDbContextAsync();
                await using var transaction =
                    await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                var existingImageCount = input.Id is { } existingId
                    ? await db.ScrumProductBacklogItemImages
                        .CountAsync(image => image.ProductBacklogItemId == existingId &&
                            image.ProductBacklogItem!.Initiative == initiative)
                    : 0;
                if (existingImageCount + imageFiles.Count > MaximumImagesPerItem)
                {
                    return (int?)null;
                }

                ScrumProductBacklogItem entity;
                if (input.Id is { } id)
                {
                    entity = await db.ScrumProductBacklogItems.SingleOrDefaultAsync(item =>
                        item.Id == id && item.Initiative == initiative)
                        ?? throw new InvalidOperationException("Product Backlog Item tidak ditemukan.");
                }
                else
                {
                    var initiativeNumber = (await db.ScrumProductBacklogItems
                        .Where(item => item.Initiative == initiative)
                        .MaxAsync(item => (int?)item.InitiativeNumber) ?? 0) + 1;
                    entity = new ScrumProductBacklogItem
                    {
                        Initiative = initiative,
                        InitiativeNumber = initiativeNumber,
                        Module = module,
                        Summary = summary,
                        AcceptanceCriteria = acceptanceCriteria,
                        Priority = input.Priority,
                        Status = "Belum dimulai",
                        TargetSprint = input.TargetSprint,
                        DefinitionOfDone = definitionOfDone
                    };
                    db.ScrumProductBacklogItems.Add(entity);
                }

                entity.Module = module;
                entity.Summary = summary;
                entity.AcceptanceCriteria = acceptanceCriteria;
                entity.Priority = input.Priority;
                entity.TargetSprint = input.TargetSprint;
                entity.DefinitionOfDone = definitionOfDone;
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
                return (int?)entity.Id;
            });
            if (itemId is null)
            {
                snackbar.Add(
                    $"Setiap PBI maksimal memiliki {MaximumImagesPerItem} gambar. Hapus gambar lama sebelum menambah lagi.",
                    Severity.Warning);
                return;
            }

            var savedImagePaths = new List<string>();
            try
            {
                if (imageFiles.Count > 0)
                {
                    await using var db = await dbContextFactory.CreateDbContextAsync();
                    var imageDirectory = imageStorage.GetDirectory(itemId.Value);
                    Directory.CreateDirectory(imageDirectory);
                    var imageEntities = new List<ScrumProductBacklogItemImage>(imageFiles.Count);
                    foreach (var image in imageFiles)
                    {
                        var fileName = $"{Guid.NewGuid():N}{image.Extension}";
                        var imagePath = Path.Combine(imageDirectory, fileName);
                        await File.WriteAllBytesAsync(imagePath, image.Content);
                        savedImagePaths.Add(imagePath);
                        imageEntities.Add(new ScrumProductBacklogItemImage
                        {
                            ProductBacklogItemId = itemId.Value,
                            FileName = fileName,
                            ContentType = image.ContentType
                        });
                    }

                    db.ScrumProductBacklogItemImages.AddRange(imageEntities);
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception exception)
            {
                DeleteImageFiles(savedImagePaths);
                logger.LogError(exception,
                    "Product Backlog Item {ProductBacklogItemId} was saved, but its images could not be uploaded.",
                itemId.Value);
                snackbar.Add(
                    "Data PBI tersimpan, tetapi gambar gagal diunggah. Periksa kapasitas dan izin folder storage, lalu coba lagi.",
                    Severity.Error);
                await LoadAsync(initiative);
                return;
            }

            snackbar.Add("Product Backlog Item berhasil disimpan.", Severity.Success);
            await LoadAsync(initiative);
        }
        catch (Exception exception)
        {
            HandleError(exception, "menyimpan Product Backlog Item",
                "Product Backlog Item gagal disimpan.");
        }
        finally
        {
            IsSaving = false;
        }
    }

    public async Task DeleteAsync(int id, string initiative = ScrumInitiative.SmartRefueling)
    {
        if (!ScrumInitiative.IsSupported(initiative))
        {
            snackbar.Add("Inisiatif Product Backlog Item tidak valid.", Severity.Warning);
            return;
        }

        IsSaving = true;
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var item = await db.ScrumProductBacklogItems
                .Include(pbi => pbi.Images)
                .SingleOrDefaultAsync(pbi => pbi.Id == id && pbi.Initiative == initiative);
            if (item is null)
            {
                snackbar.Add("Product Backlog Item tidak ditemukan. Daftar telah diperbarui.", Severity.Warning);
                await LoadAsync(initiative);
                return;
            }

            if (await db.ScrumSprints.AnyAsync(sprint =>
                    sprint.ProductBacklogItemId == id && sprint.Initiative == initiative))
            {
                snackbar.Add(
                    "Product Backlog Item tidak dapat dihapus karena masih digunakan oleh Scrum Sprint.",
                    Severity.Warning);
                return;
            }

            var imagePaths = item.Images.SelectMany(image => new[]
            {
                imageStorage.GetPath(item.Id, image.FileName),
                imageStorage.GetLegacyPath(item.Id, image.FileName)
            }).ToArray();
            db.ScrumProductBacklogItems.Remove(item);
            await db.SaveChangesAsync();
            var failedDeletes = DeleteImageFiles(imagePaths);
            snackbar.Add(
                failedDeletes == 0
                    ? "Product Backlog Item berhasil dihapus."
                    : $"Product Backlog Item berhasil dihapus, tetapi {failedDeletes} file gambar gagal dibersihkan.",
                failedDeletes == 0 ? Severity.Success : Severity.Warning);
            await LoadAsync(initiative);
        }
        catch (Exception exception)
        {
            HandleError(exception, "menghapus Product Backlog Item",
                "Product Backlog Item gagal dihapus.");
        }
        finally
        {
            IsSaving = false;
        }
    }

    public async Task DeleteImageAsync(
        int imageId,
        string initiative = ScrumInitiative.SmartRefueling)
    {
        if (!ScrumInitiative.IsSupported(initiative))
        {
            snackbar.Add("Inisiatif Product Backlog Item tidak valid.", Severity.Warning);
            return;
        }

        IsSaving = true;
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var image = await db.ScrumProductBacklogItemImages
                .SingleOrDefaultAsync(item => item.Id == imageId &&
                    item.ProductBacklogItem!.Initiative == initiative);
            if (image is null)
            {
                snackbar.Add("Gambar PBI tidak ditemukan. Daftar telah diperbarui.", Severity.Warning);
                await LoadAsync(initiative);
                return;
            }

            var imagePaths = new[]
            {
                imageStorage.GetPath(image.ProductBacklogItemId, image.FileName),
                imageStorage.GetLegacyPath(image.ProductBacklogItemId, image.FileName)
            };
            db.ScrumProductBacklogItemImages.Remove(image);
            await db.SaveChangesAsync();
            var failedDeletes = DeleteImageFiles(imagePaths);
            snackbar.Add(
                failedDeletes == 0
                    ? "Gambar PBI berhasil dihapus."
                    : "Data gambar terhapus, tetapi file gagal dibersihkan dari storage.",
                failedDeletes == 0 ? Severity.Success : Severity.Warning);
            await LoadAsync(initiative);
        }
        catch (Exception exception)
        {
            HandleError(exception, "menghapus gambar PBI", "Gambar PBI gagal dihapus.");
        }
        finally
        {
            IsSaving = false;
        }
    }

    private int DeleteImageFiles(IEnumerable<string> paths)
    {
        var failures = 0;
        foreach (var path in paths)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                failures++;
                logger.LogError(exception, "Failed to delete Product Backlog Item image at {ImagePath}.", path);
            }
        }

        return failures;
    }

    private void HandleError(Exception exception, string operation, string fallback)
    {
        if (exception.GetBaseException() is SqlException)
        {
            logger.LogError(exception, "SQL Server error while {Operation}.", operation);
            DatabaseError = "Database tidak dapat dihubungi. Periksa koneksi SQL Server, lalu coba lagi.";
        }
        else
        {
            logger.LogError(exception, "Failed while {Operation}.", operation);
            DatabaseError = fallback;
        }

        snackbar.Add(DatabaseError, Severity.Error);
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();
}
