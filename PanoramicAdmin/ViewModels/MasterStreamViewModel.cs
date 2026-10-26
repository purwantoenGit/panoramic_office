using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using PanoramicAdmin.Models;
using PanoramicAdmin.Services;

namespace PanoramicAdmin.ViewModels;

public sealed class MasterStreamViewModel(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IConfiguration configuration,
    ISnackbar snackbar,
    ILogger<MasterStreamViewModel> logger)
{
    public event Action? StateChanged;

    public IReadOnlyList<MasterStreamRow> Streams { get; private set; } = [];
    public IReadOnlyList<MasterStreamRow> FilteredStreams { get; private set; } = [];
    public string SearchText { get; private set; } = string.Empty;
    public string EditNamaStream { get; set; } = string.Empty;
    public string? DatabaseError { get; private set; }
    public string? EditError { get; private set; }
    public int? EditingStreamId { get; private set; }
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
            var streams = await db.MasterStreams.AsNoTracking()
                .OrderBy(stream => stream.Id).ToListAsync();
            Streams = streams.Select((stream, index) =>
                new MasterStreamRow(stream.Id, index + 1, stream.NamaStream)).ToList();
            ApplyFilter();
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Unable to load master streams because SQL Server is unavailable.");
            DatabaseError = "Database tidak dapat dihubungi. Periksa koneksi SQL Server, lalu coba lagi.";
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Unable to load master streams because database configuration is invalid.");
            DatabaseError = "Konfigurasi koneksi database tidak valid. Periksa pengaturan PanoramicDb.";
        }
        catch (ArgumentException exception)
        {
            logger.LogError(exception, "Unable to load master streams because database connection configuration is invalid.");
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
        FilteredStreams = Streams
            .Where(stream => stream.NamaStream.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            .Select((stream, index) => stream with { No = index + 1 })
            .ToList();
    }

    public async Task CreateAsync(string value)
    {
        IsSaving = true;
        NotifyStateChanged();
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            db.MasterStreams.Add(new MasterStream { NamaStream = value });
            await db.SaveChangesAsync();
            SearchText = string.Empty;
            snackbar.Add("Stream berhasil ditambahkan.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandlePersistenceError(exception, "menambahkan stream", "Stream gagal disimpan. Periksa data dan coba lagi.");
        }
        finally
        {
            IsSaving = false;
            NotifyStateChanged();
        }
    }

    public void BeginEdit(MasterStreamRow stream)
    {
        EditingStreamId = stream.Id;
        PendingDeleteId = null;
        EditNamaStream = stream.NamaStream;
        EditError = null;
        NotifyStateChanged();
    }

    public async Task SaveEditAsync(int id)
    {
        var value = EditNamaStream.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            EditError = "Nama stream wajib diisi.";
            NotifyStateChanged();
            return;
        }
        if (value.Length > 100)
        {
            EditError = "Nama stream maksimal 100 karakter.";
            NotifyStateChanged();
            return;
        }

        IsSaving = true;
        NotifyStateChanged();
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var stream = await db.MasterStreams.SingleOrDefaultAsync(item => item.Id == id);
            if (stream is null)
            {
                snackbar.Add("Stream tidak ditemukan. Daftar telah diperbarui.", Severity.Warning);
                await LoadAsync();
                return;
            }
            stream.NamaStream = value;
            await db.SaveChangesAsync();
            EditingStreamId = null;
            EditError = null;
            snackbar.Add("Perubahan stream berhasil disimpan.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandlePersistenceError(exception, "mengubah stream", "Perubahan stream gagal disimpan. Periksa data dan coba lagi.");
        }
        finally
        {
            IsSaving = false;
            NotifyStateChanged();
        }
    }

    public void CancelEdit()
    {
        EditingStreamId = null;
        EditError = null;
        NotifyStateChanged();
    }

    public void RequestDelete(int id)
    {
        EditingStreamId = null;
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
            var stream = await db.MasterStreams.SingleOrDefaultAsync(item => item.Id == id);
            if (stream is null)
            {
                snackbar.Add("Stream tidak ditemukan. Daftar telah diperbarui.", Severity.Warning);
                await LoadAsync();
                return;
            }
            db.MasterStreams.Remove(stream);
            await db.SaveChangesAsync();
            PendingDeleteId = null;
            snackbar.Add("Stream berhasil dihapus.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandlePersistenceError(exception, "menghapus stream", "Stream gagal dihapus. Coba lagi.");
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

public sealed record MasterStreamRow(int Id, int No, string NamaStream);
