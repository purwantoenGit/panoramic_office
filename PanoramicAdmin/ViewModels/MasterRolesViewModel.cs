using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using PanoramicAdmin.Models;
using PanoramicAdmin.Services;

namespace PanoramicAdmin.ViewModels;

public sealed class MasterRolesViewModel(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IConfiguration configuration,
    ISnackbar snackbar,
    ILogger<MasterRolesViewModel> logger)
{
    public event Action? StateChanged;

    public IReadOnlyList<MasterRoleRow> Roles { get; private set; } = [];
    public IReadOnlyList<MasterRoleRow> FilteredRoles { get; private set; } = [];
    public string SearchText { get; private set; } = string.Empty;
    public string EditRoles { get; set; } = string.Empty;
    public string? DatabaseError { get; private set; }
    public string? EditError { get; private set; }
    public int? EditingRoleId { get; private set; }
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
            var roles = await db.MasterRoles.AsNoTracking()
                .OrderBy(role => role.Id).ToListAsync();
            Roles = roles.Select((role, index) =>
                new MasterRoleRow(role.Id, index + 1, role.Roles)).ToList();
            ApplyFilter();
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Unable to load master roles because SQL Server is unavailable.");
            DatabaseError = "Database tidak dapat dihubungi. Periksa koneksi SQL Server, lalu coba lagi.";
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Unable to load master roles because database configuration is invalid.");
            DatabaseError = "Konfigurasi koneksi database tidak valid. Periksa pengaturan PanoramicDb.";
        }
        catch (ArgumentException exception)
        {
            logger.LogError(exception, "Unable to load master roles because database connection configuration is invalid.");
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
        FilteredRoles = Roles
            .Where(role => role.Roles.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            .Select((role, index) => role with { No = index + 1 })
            .ToList();
    }

    public async Task CreateAsync(string roles)
    {
        var value = roles.Trim();
        if (!Validate(value, out var validationMessage))
        {
            snackbar.Add(validationMessage, Severity.Warning);
            return;
        }

        IsSaving = true;
        NotifyStateChanged();
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            if (await db.MasterRoles.AnyAsync(role => role.Roles == value))
            {
                snackbar.Add("Role tersebut sudah terdaftar.", Severity.Warning);
                return;
            }

            db.MasterRoles.Add(new MasterRole { Roles = value });
            await db.SaveChangesAsync();
            SearchText = string.Empty;
            snackbar.Add("Role berhasil ditambahkan.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandlePersistenceError(exception, "menambahkan role", "Role gagal disimpan. Periksa data dan coba lagi.");
        }
        finally
        {
            IsSaving = false;
            NotifyStateChanged();
        }
    }

    public void BeginEdit(MasterRoleRow role)
    {
        EditingRoleId = role.Id;
        PendingDeleteId = null;
        EditRoles = role.Roles;
        EditError = null;
        NotifyStateChanged();
    }

    public async Task SaveEditAsync(int id)
    {
        var value = EditRoles.Trim();
        if (!Validate(value, out var validationMessage))
        {
            EditError = validationMessage;
            NotifyStateChanged();
            return;
        }

        EditError = null;
        IsSaving = true;
        NotifyStateChanged();
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var role = await db.MasterRoles.SingleOrDefaultAsync(item => item.Id == id);
            if (role is null)
            {
                snackbar.Add("Role tidak ditemukan. Daftar telah diperbarui.", Severity.Warning);
                await LoadAsync();
                return;
            }

            if (await db.MasterRoles.AnyAsync(item => item.Id != id && item.Roles == value))
            {
                EditError = "Role tersebut sudah terdaftar.";
                NotifyStateChanged();
                return;
            }

            var oldValue = role.Roles;
            var assignedPics = await db.MasterPics
                .Where(pic => pic.Role == oldValue)
                .ToListAsync();
            foreach (var pic in assignedPics)
            {
                pic.Role = value;
            }

            role.Roles = value;
            await db.SaveChangesAsync();
            EditingRoleId = null;
            snackbar.Add("Perubahan role berhasil disimpan.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandlePersistenceError(exception, "mengubah role", "Perubahan role gagal disimpan. Periksa data dan coba lagi.");
        }
        finally
        {
            IsSaving = false;
            NotifyStateChanged();
        }
    }

    public void CancelEdit()
    {
        EditingRoleId = null;
        EditError = null;
        NotifyStateChanged();
    }

    public void RequestDelete(int id)
    {
        EditingRoleId = null;
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
            var role = await db.MasterRoles.SingleOrDefaultAsync(item => item.Id == id);
            if (role is null)
            {
                snackbar.Add("Role tidak ditemukan. Daftar telah diperbarui.", Severity.Warning);
                await LoadAsync();
                return;
            }

            if (await db.MasterPics.AnyAsync(pic => pic.Role == role.Roles))
            {
                PendingDeleteId = null;
                snackbar.Add("Role sedang digunakan oleh Master PIC. Ubah role PIC terlebih dahulu.", Severity.Warning);
                NotifyStateChanged();
                return;
            }

            db.MasterRoles.Remove(role);
            await db.SaveChangesAsync();
            PendingDeleteId = null;
            snackbar.Add("Role berhasil dihapus.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandlePersistenceError(exception, "menghapus role", "Role gagal dihapus. Coba lagi.");
        }
        finally
        {
            IsSaving = false;
            NotifyStateChanged();
        }
    }

    private static bool Validate(string value, out string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            message = "Role wajib diisi.";
            return false;
        }

        if (value.Length > 100)
        {
            message = "Role maksimal 100 karakter.";
            return false;
        }

        message = string.Empty;
        return true;
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

public sealed record MasterRoleRow(int Id, int No, string Roles);
