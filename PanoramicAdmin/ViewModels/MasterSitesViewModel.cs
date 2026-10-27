using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using PanoramicAdmin.Components.Dialogs;
using PanoramicAdmin.Models;
using PanoramicAdmin.Services;

namespace PanoramicAdmin.ViewModels;

public sealed class MasterSitesViewModel(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IConfiguration configuration,
    ISnackbar snackbar,
    ILogger<MasterSitesViewModel> logger)
{
    public event Action? StateChanged;

    public IReadOnlyList<MasterSiteRow> Sites { get; private set; } = [];
    public IReadOnlyList<MasterSiteRow> FilteredSites { get; private set; } = [];
    public string SearchText { get; private set; } = string.Empty;
    public string EditSites { get; set; } = string.Empty;
    public string EditStatus { get; set; } = "Active";
    public string? DatabaseError { get; private set; }
    public string? EditError { get; private set; }
    public int? EditingSiteId { get; private set; }
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
            var sites = await db.MasterSites.AsNoTracking()
                .OrderBy(site => site.Sites)
                .ThenBy(site => site.Id)
                .ToListAsync();
            Sites = sites.Select((site, index) =>
                new MasterSiteRow(site.Id, index + 1, site.Sites, site.Status)).ToList();
            ApplyFilter();
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Unable to load master sites because SQL Server is unavailable.");
            DatabaseError = "Database tidak dapat dihubungi. Periksa koneksi SQL Server, lalu coba lagi.";
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Unable to load master sites because database configuration is invalid.");
            DatabaseError = "Konfigurasi koneksi database tidak valid. Periksa pengaturan PanoramicDb.";
        }
        catch (ArgumentException exception)
        {
            logger.LogError(exception, "Unable to load master sites because database connection configuration is invalid.");
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
        FilteredSites = Sites
            .Where(site => site.Sites.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || site.Status.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            .Select((site, index) => site with { No = index + 1 })
            .ToList();
    }

    public async Task CreateAsync(AddMasterSiteDialog.MasterSiteInput input)
    {
        IsSaving = true;
        NotifyStateChanged();
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            db.MasterSites.Add(new MasterSite { Sites = input.Sites, Status = input.Status });
            await db.SaveChangesAsync();
            SearchText = string.Empty;
            snackbar.Add("Site berhasil ditambahkan.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandlePersistenceError(exception, "menambahkan site", "Site gagal disimpan. Periksa data dan coba lagi.");
        }
        finally
        {
            IsSaving = false;
            NotifyStateChanged();
        }
    }

    public void BeginEdit(MasterSiteRow site)
    {
        EditingSiteId = site.Id;
        PendingDeleteId = null;
        EditSites = site.Sites;
        EditStatus = site.Status;
        EditError = null;
        NotifyStateChanged();
    }

    public async Task SaveEditAsync(int id)
    {
        var sitesValue = EditSites.Trim();
        if (string.IsNullOrWhiteSpace(sitesValue))
        {
            EditError = "Sites wajib diisi.";
            NotifyStateChanged();
            return;
        }
        if (sitesValue.Length > 150)
        {
            EditError = "Sites maksimal 150 karakter.";
            NotifyStateChanged();
            return;
        }
        if (EditStatus is not ("Active" or "Non Active"))
        {
            EditError = "Pilih status yang tersedia.";
            NotifyStateChanged();
            return;
        }

        IsSaving = true;
        NotifyStateChanged();
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var site = await db.MasterSites.SingleOrDefaultAsync(item => item.Id == id);
            if (site is null)
            {
                snackbar.Add("Site tidak ditemukan. Daftar telah diperbarui.", Severity.Warning);
                await LoadAsync();
                return;
            }
            site.Sites = sitesValue;
            site.Status = EditStatus;
            await db.SaveChangesAsync();
            EditingSiteId = null;
            EditError = null;
            snackbar.Add("Perubahan site berhasil disimpan.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandlePersistenceError(exception, "mengubah site", "Perubahan site gagal disimpan. Periksa data dan coba lagi.");
        }
        finally
        {
            IsSaving = false;
            NotifyStateChanged();
        }
    }

    public void CancelEdit()
    {
        EditingSiteId = null;
        EditError = null;
        NotifyStateChanged();
    }

    public void RequestDelete(int id)
    {
        EditingSiteId = null;
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
            var site = await db.MasterSites.SingleOrDefaultAsync(item => item.Id == id);
            if (site is null)
            {
                snackbar.Add("Site tidak ditemukan. Daftar telah diperbarui.", Severity.Warning);
                await LoadAsync();
                return;
            }
            db.MasterSites.Remove(site);
            await db.SaveChangesAsync();
            PendingDeleteId = null;
            snackbar.Add("Site berhasil dihapus.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandlePersistenceError(exception, "menghapus site", "Site gagal dihapus. Coba lagi.");
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

public sealed record MasterSiteRow(int Id, int No, string Sites, string Status);
