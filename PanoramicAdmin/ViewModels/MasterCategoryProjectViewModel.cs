using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using PanoramicAdmin.Models;
using PanoramicAdmin.Services;

namespace PanoramicAdmin.ViewModels;

public sealed class MasterCategoryProjectViewModel(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IConfiguration configuration,
    ISnackbar snackbar,
    ILogger<MasterCategoryProjectViewModel> logger)
{
    public event Action? StateChanged;

    public IReadOnlyList<MasterCategoryProjectRow> Categories { get; private set; } = [];
    public IReadOnlyList<MasterCategoryProjectRow> FilteredCategories { get; private set; } = [];
    public string SearchText { get; private set; } = string.Empty;
    public string EditCategoryProject { get; set; } = string.Empty;
    public string? DatabaseError { get; private set; }
    public string? EditError { get; private set; }
    public int? EditingCategoryProjectId { get; private set; }
    public int? PendingDeleteId { get; private set; }
    public bool IsLoading { get; private set; } = true;
    public bool IsSaving { get; private set; }

    public async Task LoadAsync()
    {
        IsLoading = true;
        DatabaseError = null;
        NotifyStateChanged();
        try
        {
            if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("PanoramicDb")))
            {
                DatabaseError = "Konfigurasi koneksi database PanoramicDb belum tersedia.";
                logger.LogError("The PanoramicDb connection string is missing or empty.");
                return;
            }

            await using var db = await dbContextFactory.CreateDbContextAsync();
            var categories = await db.MasterCategoryProjects.AsNoTracking()
                .OrderBy(category => category.Id).ToListAsync();
            Categories = categories.Select((category, index) =>
                new MasterCategoryProjectRow(category.Id, index + 1, category.CategoryProject)).ToList();
            ApplyFilter();
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Unable to load master category projects because SQL Server is unavailable.");
            DatabaseError = "Database tidak dapat dihubungi. Periksa koneksi SQL Server, lalu coba lagi.";
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Unable to load master category projects because database configuration is invalid.");
            DatabaseError = "Konfigurasi koneksi database tidak valid. Periksa pengaturan PanoramicDb.";
        }
        catch (ArgumentException exception)
        {
            logger.LogError(exception, "Unable to load master category projects because database connection configuration is invalid.");
            DatabaseError = "Konfigurasi koneksi database tidak valid. Periksa pengaturan PanoramicDb.";
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    public void Search(string? text)
    {
        SearchText = text ?? string.Empty;
        ApplyFilter();
        NotifyStateChanged();
    }

    private void ApplyFilter()
    {
        FilteredCategories = Categories
            .Where(category => category.CategoryProject.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            .Select((category, index) => category with { No = index + 1 })
            .ToList();
    }

    public async Task CreateAsync(string value)
    {
        IsSaving = true;
        NotifyStateChanged();
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            db.MasterCategoryProjects.Add(new MasterCategoryProject { CategoryProject = value });
            await db.SaveChangesAsync();
            SearchText = string.Empty;
            snackbar.Add("Kategori project berhasil ditambahkan.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandlePersistenceError(exception, "menambahkan kategori project", "Kategori project gagal disimpan. Periksa data dan coba lagi.");
        }
        finally
        {
            IsSaving = false;
            NotifyStateChanged();
        }
    }

    public void BeginEdit(MasterCategoryProjectRow category)
    {
        EditingCategoryProjectId = category.Id;
        PendingDeleteId = null;
        EditCategoryProject = category.CategoryProject;
        EditError = null;
        NotifyStateChanged();
    }

    public async Task SaveEditAsync(int id)
    {
        var value = EditCategoryProject.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            EditError = "Kategori project wajib diisi.";
            NotifyStateChanged();
            return;
        }
        if (value.Length > 50)
        {
            EditError = "Kategori project maksimal 50 karakter.";
            NotifyStateChanged();
            return;
        }

        IsSaving = true;
        NotifyStateChanged();
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var category = await db.MasterCategoryProjects.SingleOrDefaultAsync(item => item.Id == id);
            if (category is null)
            {
                snackbar.Add("Kategori project tidak ditemukan. Daftar telah diperbarui.", Severity.Warning);
                await LoadAsync();
                return;
            }
            category.CategoryProject = value;
            await db.SaveChangesAsync();
            EditingCategoryProjectId = null;
            EditError = null;
            snackbar.Add("Perubahan kategori project berhasil disimpan.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandlePersistenceError(exception, "mengubah kategori project", "Perubahan kategori project gagal disimpan. Periksa data dan coba lagi.");
        }
        finally
        {
            IsSaving = false;
            NotifyStateChanged();
        }
    }

    public void CancelEdit()
    {
        EditingCategoryProjectId = null;
        EditError = null;
        NotifyStateChanged();
    }

    public void RequestDelete(int id)
    {
        EditingCategoryProjectId = null;
        PendingDeleteId = id;
        NotifyStateChanged();
    }

    public void CancelDelete()
    {
        PendingDeleteId = null;
        NotifyStateChanged();
    }

    public async Task DeleteAsync(int id)
    {
        IsSaving = true;
        NotifyStateChanged();
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var category = await db.MasterCategoryProjects.SingleOrDefaultAsync(item => item.Id == id);
            if (category is null)
            {
                snackbar.Add("Kategori project tidak ditemukan. Daftar telah diperbarui.", Severity.Warning);
                await LoadAsync();
                return;
            }
            db.MasterCategoryProjects.Remove(category);
            await db.SaveChangesAsync();
            PendingDeleteId = null;
            snackbar.Add("Kategori project berhasil dihapus.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandlePersistenceError(exception, "menghapus kategori project", "Kategori project gagal dihapus. Coba lagi.");
        }
        finally
        {
            IsSaving = false;
            NotifyStateChanged();
        }
    }

    private void HandlePersistenceError(Exception exception, string operation, string fallbackMessage)
    {
        if (exception.GetBaseException() is SqlException)
        {
            logger.LogError(exception, "SQL Server error while {Operation}.", operation);
            DatabaseError = "Database tidak dapat dihubungi. Periksa koneksi SQL Server, lalu coba lagi.";
            snackbar.Add(DatabaseError, Severity.Error);
        }
        else if (exception is InvalidOperationException or ArgumentException)
        {
            logger.LogError(exception, "Database configuration error while {Operation}.", operation);
            DatabaseError = "Konfigurasi koneksi database tidak valid. Periksa pengaturan PanoramicDb.";
            snackbar.Add(DatabaseError, Severity.Error);
        }
        else
        {
            logger.LogError(exception, "Failed while {Operation}.", operation);
            snackbar.Add(fallbackMessage, Severity.Error);
        }
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();
}

public sealed record MasterCategoryProjectRow(int Id, int No, string CategoryProject);
