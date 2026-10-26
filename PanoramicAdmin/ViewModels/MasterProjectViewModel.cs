using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using PanoramicAdmin.Models;
using PanoramicAdmin.Services;

namespace PanoramicAdmin.ViewModels;

public sealed class MasterProjectViewModel(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IConfiguration configuration,
    ISnackbar snackbar,
    ILogger<MasterProjectViewModel> logger)
{
    private string[] _editingOriginalSites = [];

    public event Action? StateChanged;

    public IReadOnlyList<ProjectRow> Projects { get; private set; } = [];
    public IReadOnlyList<ProjectRow> FilteredProjects { get; private set; } = [];
    public IReadOnlyList<string> MasterStreams { get; private set; } = [];
    public IReadOnlyList<string> MasterCategoryProjects { get; private set; } = [];
    public IReadOnlyList<string> MasterSites { get; private set; } = [];

    public string SearchText { get; private set; } = string.Empty;
    public int? EditingProjectId { get; private set; }
    public int? PendingDeleteId { get; private set; }
    public string EditName { get; set; } = string.Empty;
    public string EditStream { get; set; } = string.Empty;
    public string EditCategoryProject { get; set; } = string.Empty;
    public IReadOnlyCollection<string> EditSites { get; set; } = Array.Empty<string>();
    public string? EditError { get; private set; }
    public string? DatabaseError { get; private set; }
    public bool IsLoading { get; private set; } = true;
    public bool IsSaving { get; private set; }

    public async Task LoadAsync()
    {
        IsLoading = true;
        DatabaseError = null;
        MasterStreams = [];
        MasterCategoryProjects = [];
        MasterSites = [];
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
            await RefreshMasterOptionsAsync(db);

            var projects = await db.Projects
                .AsNoTracking()
                .Include(project => project.Sites)
                .OrderBy(project => project.Id)
                .ToListAsync();

            Projects = projects
                .Select((project, index) => new ProjectRow(
                    project.Id,
                    index + 1,
                    project.Name,
                    project.Stream,
                    project.CategoryProject,
                    project.Sites.Select(site => site.Name).ToArray()))
                .ToList();
            ApplyFilter();
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Unable to load master projects because SQL Server is unavailable.");
            DatabaseError = "Database tidak dapat dihubungi. Periksa koneksi SQL Server, lalu coba lagi.";
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Unable to load master projects because database configuration is invalid.");
            DatabaseError = "Konfigurasi koneksi database tidak valid. Periksa pengaturan PanoramicDb.";
        }
        catch (ArgumentException exception)
        {
            logger.LogError(exception, "Unable to load master projects because the database connection configuration is invalid.");
            DatabaseError = "Konfigurasi koneksi database tidak valid. Periksa pengaturan PanoramicDb.";
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    public async Task RefreshMasterOptionsAsync()
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            await RefreshMasterOptionsAsync(db);
            NotifyStateChanged();
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Unable to refresh master project options.");
            DatabaseError = "Database tidak dapat dihubungi. Periksa koneksi SQL Server, lalu coba lagi.";
            NotifyStateChanged();
        }
    }

    private async Task RefreshMasterOptionsAsync(AppDbContext db)
    {
        MasterStreams = await db.MasterStreams
            .AsNoTracking()
            .OrderBy(stream => stream.NamaStream)
            .Select(stream => stream.NamaStream)
            .ToListAsync();
        MasterCategoryProjects = await db.MasterCategoryProjects
            .AsNoTracking()
            .OrderBy(category => category.CategoryProject)
            .Select(category => category.CategoryProject)
            .ToListAsync();
        MasterSites = await db.MasterSites
            .AsNoTracking()
            .Where(site => site.Status == "Active")
            .OrderBy(site => site.Sites)
            .Select(site => site.Sites)
            .ToListAsync();
    }

    public void Search(string? text)
    {
        SearchText = text ?? string.Empty;
        ApplyFilter();
        NotifyStateChanged();
    }

    private void ApplyFilter()
    {
        FilteredProjects = string.IsNullOrWhiteSpace(SearchText)
            ? Projects
            : Projects.Where(project =>
                    project.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    project.Stream.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    project.Sites.Any(site => site.Contains(SearchText, StringComparison.OrdinalIgnoreCase)))
                .ToList();
    }

    public bool IsAvailableStream(string? stream) =>
        !string.IsNullOrWhiteSpace(stream) &&
        MasterStreams.Contains(stream, StringComparer.Ordinal);

    public bool IsAvailableCategoryProject(string? categoryProject) =>
        !string.IsNullOrWhiteSpace(categoryProject) &&
        MasterCategoryProjects.Contains(categoryProject, StringComparer.Ordinal);

    public async Task CreateAsync(ProjectInput input)
    {
        if (!IsAvailableStream(input.Stream))
        {
            snackbar.Add("Pilih stream yang tersedia dari Master Stream.", Severity.Warning);
            return;
        }

        if (!IsAvailableCategoryProject(input.CategoryProject))
        {
            snackbar.Add("Pilih kategori project yang tersedia dari Master Category Project.", Severity.Warning);
            return;
        }

        IsSaving = true;
        NotifyStateChanged();
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            if (!await db.MasterStreams.AnyAsync(stream => stream.NamaStream == input.Stream))
            {
                snackbar.Add("Stream tidak lagi tersedia. Buka kembali form dan pilih stream terbaru.", Severity.Warning);
                await LoadAsync();
                return;
            }

            if (!await db.MasterCategoryProjects.AnyAsync(category => category.CategoryProject == input.CategoryProject))
            {
                snackbar.Add("Kategori project tidak lagi tersedia. Buka kembali form dan pilih kategori terbaru.", Severity.Warning);
                await LoadAsync();
                return;
            }

            var activeSites = await db.MasterSites
                .AsNoTracking()
                .Where(site => site.Status == "Active")
                .Select(site => site.Sites)
                .ToListAsync();
            if (input.Sites.Length == 0 ||
                input.Sites.Distinct(StringComparer.Ordinal).Count() != input.Sites.Length ||
                input.Sites.Any(name => !activeSites.Contains(name, StringComparer.Ordinal)))
            {
                snackbar.Add("Satu atau beberapa site tidak lagi aktif. Buka kembali form dan pilih site aktif terbaru.", Severity.Warning);
                await LoadAsync();
                return;
            }

            db.Projects.Add(new Project
            {
                Name = input.Name,
                Stream = input.Stream,
                CategoryProject = input.CategoryProject,
                Sites = input.Sites
                    .Select(name => new ProjectSite { Name = name })
                    .ToList()
            });
            await db.SaveChangesAsync();

            SearchText = string.Empty;
            snackbar.Add("Project berhasil ditambahkan.", Severity.Success);
            await LoadAsync();
        }
        catch (DbUpdateException exception)
        {
            if (exception.GetBaseException() is SqlException)
            {
                SetSaveDatabaseError(exception, "SQL Server gagal menyimpan project baru.");
            }
            else
            {
                logger.LogError(exception, "Failed to save a new master project.");
                snackbar.Add("Project gagal disimpan. Periksa data dan coba lagi.", Severity.Error);
            }
        }
        catch (SqlException exception)
        {
            SetSaveDatabaseError(exception, "SQL Server tidak dapat dihubungi saat menambahkan project.");
        }
        catch (InvalidOperationException exception)
        {
            SetSaveConfigurationError(exception, "menambahkan project");
        }
        catch (ArgumentException exception)
        {
            SetSaveConfigurationError(exception, "menambahkan project");
        }
        finally
        {
            IsSaving = false;
            NotifyStateChanged();
        }
    }

    public void BeginEdit(ProjectRow project)
    {
        if (MasterStreams.Count == 0 || MasterCategoryProjects.Count == 0 || MasterSites.Count == 0)
        {
            return;
        }

        EditingProjectId = project.Id;
        PendingDeleteId = null;
        EditName = project.Name;
        EditStream = project.Stream;
        EditCategoryProject = project.CategoryProject;
        _editingOriginalSites = project.Sites.ToArray();
        EditSites = _editingOriginalSites.ToArray();
        EditError = null;
        NotifyStateChanged();
    }

    public async Task SaveEditAsync(int id)
    {
        var sites = EditSites
            .Where(name => MasterSites.Contains(name, StringComparer.Ordinal) ||
                           _editingOriginalSites.Contains(name, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (string.IsNullOrWhiteSpace(EditName) ||
            !IsAvailableStream(EditStream) ||
            !IsAvailableCategoryProject(EditCategoryProject) ||
            sites.Length == 0 ||
            sites.Length != EditSites.Count())
        {
            EditError = "Nama, stream, kategori project dari daftar master, dan minimal satu site yang diizinkan harus dipilih.";
            NotifyStateChanged();
            return;
        }

        IsSaving = true;
        NotifyStateChanged();
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            if (!await db.MasterStreams.AnyAsync(stream => stream.NamaStream == EditStream))
            {
                EditError = "Stream tidak lagi tersedia. Pilih stream terbaru dari daftar.";
                await LoadAsync();
                return;
            }

            if (!await db.MasterCategoryProjects.AnyAsync(category => category.CategoryProject == EditCategoryProject))
            {
                EditError = "Kategori project tidak lagi tersedia. Pilih kategori terbaru dari daftar.";
                await LoadAsync();
                return;
            }

            var project = await db.Projects
                .Include(item => item.Sites)
                .SingleOrDefaultAsync(item => item.Id == id);

            if (project is null)
            {
                snackbar.Add("Project tidak ditemukan. Daftar telah diperbarui.", Severity.Warning);
                await LoadAsync();
                return;
            }

            var activeSites = await db.MasterSites
                .AsNoTracking()
                .Where(site => site.Status == "Active")
                .Select(site => site.Sites)
                .ToListAsync();
            var originalSites = project.Sites.Select(site => site.Name).ToArray();
            if (sites.Length == 0 ||
                sites.Distinct(StringComparer.Ordinal).Count() != sites.Length ||
                sites.Any(name => !activeSites.Contains(name, StringComparer.Ordinal) &&
                                  !originalSites.Contains(name, StringComparer.Ordinal)))
            {
                EditError = "Satu atau beberapa site tidak lagi aktif atau bukan site project ini. Pilih ulang site yang diizinkan.";
                await LoadAsync();
                return;
            }

            project.Name = EditName.Trim();
            project.Stream = EditStream.Trim();
            project.CategoryProject = EditCategoryProject;
            db.ProjectSites.RemoveRange(project.Sites);
            project.Sites = sites
                .Select(name => new ProjectSite { ProjectId = project.Id, Name = name })
                .ToList();

            await db.SaveChangesAsync();
            EditingProjectId = null;
            EditError = null;
            snackbar.Add("Perubahan project berhasil disimpan.", Severity.Success);
            await LoadAsync();
        }
        catch (DbUpdateException exception)
        {
            if (exception.GetBaseException() is SqlException)
            {
                SetSaveDatabaseError(exception, "SQL Server gagal menyimpan perubahan project.");
            }
            else
            {
                logger.LogError(exception, "Failed to update master project {ProjectId}.", id);
                snackbar.Add("Perubahan project gagal disimpan. Periksa data dan coba lagi.", Severity.Error);
            }
        }
        catch (SqlException exception)
        {
            SetSaveDatabaseError(exception, "SQL Server tidak dapat dihubungi saat mengubah project.");
        }
        catch (InvalidOperationException exception)
        {
            SetSaveConfigurationError(exception, "mengubah project");
        }
        catch (ArgumentException exception)
        {
            SetSaveConfigurationError(exception, "mengubah project");
        }
        finally
        {
            IsSaving = false;
            NotifyStateChanged();
        }
    }

    public void CancelEdit()
    {
        EditingProjectId = null;
        EditError = null;
        NotifyStateChanged();
    }

    public void RequestDelete(int id)
    {
        EditingProjectId = null;
        PendingDeleteId = id;
        NotifyStateChanged();
    }

    public void CancelDelete()
    {
        PendingDeleteId = null;
        NotifyStateChanged();
    }

    public async Task DeleteProjectAsync(int id)
    {
        IsSaving = true;
        NotifyStateChanged();
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var project = await db.Projects.FindAsync(id);

            if (project is null)
            {
                PendingDeleteId = null;
                snackbar.Add("Project tidak ditemukan. Daftar telah diperbarui.", Severity.Warning);
                await LoadAsync();
                return;
            }

            db.Projects.Remove(project);
            await db.SaveChangesAsync();

            PendingDeleteId = null;
            snackbar.Add("Project berhasil dihapus.", Severity.Success);
            await LoadAsync();
        }
        catch (DbUpdateException exception)
        {
            if (exception.GetBaseException() is SqlException)
            {
                SetSaveDatabaseError(exception, "SQL Server gagal menghapus project.");
            }
            else
            {
                logger.LogError(exception, "Failed to delete master project {ProjectId}.", id);
                snackbar.Add("Project gagal dihapus. Periksa relasi data dan coba lagi.", Severity.Error);
            }
        }
        catch (SqlException exception)
        {
            SetSaveDatabaseError(exception, "SQL Server tidak dapat dihubungi saat menghapus project.");
        }
        catch (InvalidOperationException exception)
        {
            SetSaveConfigurationError(exception, "menghapus project");
        }
        catch (ArgumentException exception)
        {
            SetSaveConfigurationError(exception, "menghapus project");
        }
        finally
        {
            IsSaving = false;
            NotifyStateChanged();
        }
    }

    public IEnumerable<string> GetEditSiteOptions(ProjectRow project) =>
        MasterSites
            .Concat(project.Sites.Where(site => !MasterSites.Contains(site, StringComparer.Ordinal)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(site => site, StringComparer.Ordinal);

    public string GetEditSiteLabel(string site) =>
        MasterSites.Contains(site, StringComparer.Ordinal) ? site : $"{site} (Non Active)";

    public async Task<bool> PrepareCreateDialogAsync()
    {
        await LoadAsync();
        if (DatabaseError is not null)
        {
            snackbar.Add(DatabaseError, Severity.Error);
            return false;
        }

        if (MasterStreams.Count == 0)
        {
            snackbar.Add("Tambahkan stream di halaman Master Stream sebelum membuat project.", Severity.Warning);
            return false;
        }

        if (MasterCategoryProjects.Count == 0)
        {
            snackbar.Add("Tambahkan kategori project di halaman Master Category Project sebelum membuat project.", Severity.Warning);
            return false;
        }

        if (MasterSites.Count == 0)
        {
            snackbar.Add("Tambahkan atau aktifkan site di halaman Master Sites sebelum membuat project.", Severity.Warning);
            return false;
        }

        return true;
    }

    private void SetSaveDatabaseError(Exception exception, string logMessage)
    {
        logger.LogError(exception, "{DatabaseError}", logMessage);
        DatabaseError = "Database tidak dapat dihubungi. Periksa koneksi SQL Server, lalu coba lagi.";
        snackbar.Add(DatabaseError, Severity.Error);
    }

    private void SetSaveConfigurationError(Exception exception, string operation)
    {
        logger.LogError(exception, "Database configuration error while {Operation}.", operation);
        DatabaseError = "Konfigurasi koneksi database tidak valid. Periksa pengaturan PanoramicDb.";
        snackbar.Add(DatabaseError, Severity.Error);
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();
}

public sealed record ProjectRow(
    int Id,
    int No,
    string Name,
    string Stream,
    string CategoryProject,
    string[] Sites);
