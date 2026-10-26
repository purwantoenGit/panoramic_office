using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using PanoramicAdmin.Models;
using PanoramicAdmin.Services;

namespace PanoramicAdmin.ViewModels;

public sealed class StrategicActionViewModel(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IConfiguration configuration,
    ISnackbar snackbar,
    ILogger<StrategicActionViewModel> logger)
{
    public event Action? StateChanged;
    public IReadOnlyList<StrategicActionRow> Actions { get; private set; } = [];
    public IReadOnlyList<StrategicActionRow> FilteredActions { get; private set; } = [];
    public string SearchText { get; private set; } = string.Empty;
    public string? DatabaseError { get; private set; }
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
            var actions = await db.StrategicActions.AsNoTracking()
                .OrderBy(action => action.Id)
                .ToListAsync();
            Actions = actions.Select((action, index) =>
                new StrategicActionRow(action.Id, index + 1, action.NamaStrategicAction)).ToList();
            ApplyFilter();
        }
        catch (Exception exception)
        {
            HandleError(exception, "memuat Strategic Action", "Strategic Action gagal dimuat.");
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
        FilteredActions = Actions
            .Where(action => action.NamaStrategicAction.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            .Select((action, index) => action with { No = index + 1 })
            .ToList();
    }

    public async Task CreateAsync(string nama) => await PersistAsync(async db =>
    {
        var value = nama.Trim();
        if (!Validate(value)) return false;
        db.StrategicActions.Add(new StrategicAction { NamaStrategicAction = value });
        await db.SaveChangesAsync();
        snackbar.Add("Strategic Action berhasil ditambahkan.", Severity.Success);
        SearchText = string.Empty;
        return true;
    }, "menambahkan Strategic Action", "Strategic Action gagal disimpan. Periksa data dan coba lagi.");

    public async Task UpdateAsync(int id, string nama) => await PersistAsync(async db =>
    {
        var value = nama.Trim();
        if (!Validate(value)) return false;
        var action = await db.StrategicActions.SingleOrDefaultAsync(item => item.Id == id);
        if (action is null)
        {
            snackbar.Add("Strategic Action tidak ditemukan. Daftar telah diperbarui.", Severity.Warning);
            return false;
        }
        action.NamaStrategicAction = value;
        await db.SaveChangesAsync();
        snackbar.Add("Perubahan Strategic Action berhasil disimpan.", Severity.Success);
        return true;
    }, "mengubah Strategic Action", "Perubahan gagal disimpan. Periksa data dan coba lagi.");

    public void RequestDelete(int id)
    {
        PendingDeleteId = id;
        NotifyStateChanged();
    }

    public void CancelDelete()
    {
        PendingDeleteId = null;
        NotifyStateChanged();
    }

    public async Task DeleteAsync(int id) => await PersistAsync(async db =>
    {
        var action = await db.StrategicActions.SingleOrDefaultAsync(item => item.Id == id);
        if (action is null)
        {
            snackbar.Add("Strategic Action tidak ditemukan. Daftar telah diperbarui.", Severity.Warning);
            return false;
        }
        db.StrategicActions.Remove(action);
        await db.SaveChangesAsync();
        PendingDeleteId = null;
        snackbar.Add("Strategic Action berhasil dihapus.", Severity.Success);
        return true;
    }, "menghapus Strategic Action", "Strategic Action tidak dapat dihapus. Pastikan tidak sedang digunakan oleh Project Issue.");

    private bool Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200)
        {
            snackbar.Add(string.IsNullOrWhiteSpace(value)
                ? "Nama Strategic Action wajib diisi."
                : "Nama Strategic Action maksimal 200 karakter.", Severity.Warning);
            return false;
        }
        return true;
    }

    private async Task PersistAsync(Func<AppDbContext, Task<bool>> operation, string description, string fallback)
    {
        IsSaving = true;
        NotifyStateChanged();
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            if (await operation(db))
                await LoadAsync();
        }
        catch (Exception exception)
        {
            HandleError(exception, description, fallback);
        }
        finally
        {
            IsSaving = false;
            NotifyStateChanged();
        }
    }

    private void HandleError(Exception exception, string operation, string fallback)
    {
        if (exception.GetBaseException() is SqlException)
        {
            logger.LogError(exception, "SQL Server error while {Operation}.", operation);
            DatabaseError = "Database tidak dapat dihubungi. Periksa koneksi SQL Server, lalu coba lagi.";
            snackbar.Add(DatabaseError, Severity.Error);
        }
        else
        {
            logger.LogError(exception, "Failed while {Operation}.", operation);
            DatabaseError = fallback;
            snackbar.Add(fallback, Severity.Error);
        }
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();
}

public sealed record StrategicActionRow(int Id, int No, string NamaStrategicAction);
