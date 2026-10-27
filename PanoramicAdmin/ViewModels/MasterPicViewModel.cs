using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using PanoramicAdmin.Models;
using PanoramicAdmin.Services;

namespace PanoramicAdmin.ViewModels;

public sealed class MasterPicViewModel(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IConfiguration configuration,
    ISnackbar snackbar,
    ILogger<MasterPicViewModel> logger)
{
    public event Action? StateChanged;

    public IReadOnlyList<MasterPicRow> Pics { get; private set; } = [];
    public IReadOnlyList<MasterPicRow> FilteredPics { get; private set; } = [];
    public IReadOnlyList<string> AvailableRoles { get; private set; } = [];
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
            AvailableRoles = await db.MasterRoles.AsNoTracking()
                .OrderBy(role => role.Roles)
                .Select(role => role.Roles)
                .ToListAsync();
            var pics = await db.MasterPics.AsNoTracking()
                .OrderBy(pic => pic.Nama)
                .ThenBy(pic => pic.Id)
                .ToListAsync();
            Pics = pics.Select((pic, index) =>
                new MasterPicRow(pic.Id, index + 1, pic.Nama, pic.Role)).ToList();
            ApplyFilter();
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Unable to load master PICs because SQL Server is unavailable.");
            DatabaseError = "Database tidak dapat dihubungi. Periksa koneksi SQL Server, lalu coba lagi.";
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Unable to load master PICs because database configuration is invalid.");
            DatabaseError = "Konfigurasi koneksi database tidak valid. Periksa pengaturan PanoramicDb.";
        }
        catch (ArgumentException exception)
        {
            logger.LogError(exception, "Unable to load master PICs because database connection configuration is invalid.");
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
        FilteredPics = Pics
            .Where(pic =>
                pic.Nama.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                pic.Role.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            .Select((pic, index) => pic with { No = index + 1 })
            .ToList();
    }

    public async Task CreateAsync(MasterPicInput input)
    {
        var nama = input.Nama.Trim();
        var role = input.Role.Trim();
        if (string.IsNullOrWhiteSpace(nama) || nama.Length > 150 ||
            string.IsNullOrWhiteSpace(role) || !AvailableRoles.Contains(role, StringComparer.Ordinal))
        {
            var message = string.IsNullOrWhiteSpace(nama)
                ? "Nama wajib diisi."
                : nama.Length > 150
                    ? "Nama maksimal 150 karakter."
                    : string.IsNullOrWhiteSpace(role)
                        ? "Role wajib dipilih."
                        : "Pilih role yang tersedia dari Master Roles.";
            snackbar.Add(message, Severity.Warning);
            return;
        }

        IsSaving = true;
        NotifyStateChanged();
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            if (!await db.MasterRoles.AnyAsync(item => item.Roles == role))
            {
                snackbar.Add("Role tidak lagi tersedia. Pilih role terbaru dari Master Roles.", Severity.Warning);
                await LoadAsync();
                return;
            }

            db.MasterPics.Add(new MasterPic { Nama = nama, Role = role });
            await db.SaveChangesAsync();
            SearchText = string.Empty;
            snackbar.Add("PIC berhasil ditambahkan.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandlePersistenceError(exception, "menambahkan PIC", "PIC gagal disimpan. Periksa data dan coba lagi.");
        }
        finally
        {
            IsSaving = false;
            NotifyStateChanged();
        }
    }

    public async Task UpdateAsync(int id, MasterPicInput input)
    {
        var nama = input.Nama.Trim();
        var role = input.Role.Trim();
        if (string.IsNullOrWhiteSpace(nama))
        {
            snackbar.Add("Nama wajib diisi.", Severity.Warning);
            return;
        }
        if (nama.Length > 150)
        {
            snackbar.Add("Nama maksimal 150 karakter.", Severity.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(role))
        {
            snackbar.Add("Role wajib dipilih.", Severity.Warning);
            return;
        }
        if (!AvailableRoles.Contains(role, StringComparer.Ordinal))
        {
            snackbar.Add("Pilih role yang tersedia dari Master Roles.", Severity.Warning);
            return;
        }

        IsSaving = true;
        NotifyStateChanged();
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var pic = await db.MasterPics.SingleOrDefaultAsync(item => item.Id == id);
            if (pic is null)
            {
                snackbar.Add("PIC tidak ditemukan. Daftar telah diperbarui.", Severity.Warning);
                await LoadAsync();
                return;
            }

            if (!await db.MasterRoles.AnyAsync(item => item.Roles == role))
            {
                snackbar.Add("Role tidak lagi tersedia. Pilih role terbaru dari Master Roles.", Severity.Warning);
                await LoadAsync();
                return;
            }

            pic.Nama = nama;
            pic.Role = role;
            await db.SaveChangesAsync();
            snackbar.Add("Perubahan PIC berhasil disimpan.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandlePersistenceError(exception, "mengubah PIC", "Perubahan PIC gagal disimpan. Periksa data dan coba lagi.");
        }
        finally
        {
            IsSaving = false;
            NotifyStateChanged();
        }
    }

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

    public async Task DeleteAsync(int id)
    {
        IsSaving = true;
        NotifyStateChanged();
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var pic = await db.MasterPics.SingleOrDefaultAsync(item => item.Id == id);
            if (pic is null)
            {
                snackbar.Add("PIC tidak ditemukan. Daftar telah diperbarui.", Severity.Warning);
                await LoadAsync();
                return;
            }

            db.MasterPics.Remove(pic);
            await db.SaveChangesAsync();
            PendingDeleteId = null;
            snackbar.Add("PIC berhasil dihapus.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandlePersistenceError(exception, "menghapus PIC", "PIC gagal dihapus. Coba lagi.");
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

public sealed record MasterPicRow(int Id, int No, string Nama, string Role);
