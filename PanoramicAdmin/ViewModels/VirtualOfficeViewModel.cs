using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PanoramicAdmin.Services;

namespace PanoramicAdmin.ViewModels;

public sealed class VirtualOfficeViewModel(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IConfiguration configuration,
    ILogger<VirtualOfficeViewModel> logger)
{
    private const string ProjectImplementorRole = "Project Implementor";

    public event Action? StateChanged;

    public IReadOnlyList<ProjectImplementorPic> ProjectImplementors { get; private set; } = [];
    public string? DatabaseError { get; private set; }
    public bool IsLoading { get; private set; } = true;

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
            ProjectImplementors = await db.MasterPics.AsNoTracking()
                .Where(pic => pic.Role == ProjectImplementorRole)
                .OrderBy(pic => pic.Nama)
                .Select(pic => new ProjectImplementorPic(pic.Id, pic.Nama))
                .ToListAsync();
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Unable to load Project Implementor PICs for the Virtual Office.");
            DatabaseError = "Data PIC tidak dapat dimuat. Periksa koneksi database dan skema Master PIC.";
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Unable to load Project Implementor PICs because database configuration is invalid.");
            DatabaseError = "Konfigurasi koneksi database tidak valid. Periksa pengaturan PanoramicDb.";
        }
        catch (ArgumentException exception)
        {
            logger.LogError(exception, "Unable to load Project Implementor PICs because database connection configuration is invalid.");
            DatabaseError = "Konfigurasi koneksi database tidak valid. Periksa pengaturan PanoramicDb.";
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();
}

public sealed record ProjectImplementorPic(int Id, string Nama);
