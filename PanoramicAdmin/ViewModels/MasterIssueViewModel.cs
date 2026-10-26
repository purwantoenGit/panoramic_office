using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using PanoramicAdmin.Models;
using PanoramicAdmin.Services;

namespace PanoramicAdmin.ViewModels;

public sealed class MasterIssueViewModel(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IConfiguration configuration,
    ISnackbar snackbar,
    ILogger<MasterIssueViewModel> logger)
{
    public event Action? StateChanged;

    public IReadOnlyList<MasterIssueRow> Issues { get; private set; } = [];
    public IReadOnlyList<MasterIssueRow> FilteredIssues { get; private set; } = [];
    public string SearchText { get; private set; } = string.Empty;
    public string EditIssue { get; set; } = string.Empty;
    public bool EditActive { get; set; } = true;
    public string? DatabaseError { get; private set; }
    public string? EditError { get; private set; }
    public int? EditingIssueId { get; private set; }
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
            var issues = await db.MasterIssues.AsNoTracking()
                .OrderBy(issue => issue.Id).ToListAsync();
            Issues = issues.Select((issue, index) =>
                new MasterIssueRow(issue.Id, index + 1, issue.Issue, issue.Active)).ToList();
            ApplyFilter();
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Unable to load master issues because SQL Server is unavailable.");
            DatabaseError = "Database tidak dapat dihubungi. Periksa koneksi SQL Server, lalu coba lagi.";
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Unable to load master issues because database configuration is invalid.");
            DatabaseError = "Konfigurasi koneksi database tidak valid. Periksa pengaturan PanoramicDb.";
        }
        catch (ArgumentException exception)
        {
            logger.LogError(exception, "Unable to load master issues because database connection configuration is invalid.");
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
        FilteredIssues = Issues
            .Where(issue => issue.Issue.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            .Select((issue, index) => issue with { No = index + 1 })
            .ToList();
    }

    public async Task CreateAsync(string issue, bool active)
    {
        var value = issue.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200)
        {
            snackbar.Add(
                string.IsNullOrWhiteSpace(value) ? "Issue wajib diisi." : "Issue maksimal 200 karakter.",
                Severity.Warning);
            return;
        }

        IsSaving = true;
        NotifyStateChanged();
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            db.MasterIssues.Add(new MasterIssue { Issue = value, Active = active });
            await db.SaveChangesAsync();
            SearchText = string.Empty;
            snackbar.Add("Master Issue berhasil ditambahkan.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandlePersistenceError(exception, "menambahkan Master Issue", "Master Issue gagal disimpan. Periksa data dan coba lagi.");
        }
        finally
        {
            IsSaving = false;
            NotifyStateChanged();
        }
    }

    public void BeginEdit(MasterIssueRow issue)
    {
        EditingIssueId = issue.Id;
        PendingDeleteId = null;
        EditIssue = issue.Issue;
        EditActive = issue.Active;
        EditError = null;
        NotifyStateChanged();
    }

    public async Task SaveEditAsync(int id)
    {
        var value = EditIssue.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            EditError = "Issue wajib diisi.";
            NotifyStateChanged();
            return;
        }
        if (value.Length > 200)
        {
            EditError = "Issue maksimal 200 karakter.";
            NotifyStateChanged();
            return;
        }

        EditError = null;
        IsSaving = true;
        NotifyStateChanged();
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var issue = await db.MasterIssues.SingleOrDefaultAsync(item => item.Id == id);
            if (issue is null)
            {
                snackbar.Add("Issue tidak ditemukan. Daftar telah diperbarui.", Severity.Warning);
                await LoadAsync();
                return;
            }

            issue.Issue = value;
            issue.Active = EditActive;
            await db.SaveChangesAsync();
            EditingIssueId = null;
            snackbar.Add("Perubahan Master Issue berhasil disimpan.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandlePersistenceError(exception, "mengubah Master Issue", "Perubahan Master Issue gagal disimpan. Periksa data dan coba lagi.");
        }
        finally
        {
            IsSaving = false;
            NotifyStateChanged();
        }
    }

    public void CancelEdit()
    {
        EditingIssueId = null;
        EditError = null;
        NotifyStateChanged();
    }

    public void RequestDelete(int id)
    {
        EditingIssueId = null;
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
            var issue = await db.MasterIssues.SingleOrDefaultAsync(item => item.Id == id);
            if (issue is null)
            {
                snackbar.Add("Issue tidak ditemukan. Daftar telah diperbarui.", Severity.Warning);
                await LoadAsync();
                return;
            }

            db.MasterIssues.Remove(issue);
            await db.SaveChangesAsync();
            PendingDeleteId = null;
            snackbar.Add("Master Issue berhasil dihapus.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandlePersistenceError(exception, "menghapus Master Issue", "Master Issue gagal dihapus. Coba lagi.");
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

public sealed record MasterIssueRow(int Id, int No, string Issue, bool Active);
