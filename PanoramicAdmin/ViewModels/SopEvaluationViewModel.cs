using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using PanoramicAdmin.Models;
using PanoramicAdmin.Services;

namespace PanoramicAdmin.ViewModels;

public sealed class SopEvaluationViewModel(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IConfiguration configuration,
    ISnackbar snackbar,
    ILogger<SopEvaluationViewModel> logger)
{
    private IReadOnlyList<SopEvaluation> _evaluations = [];

    public event Action? StateChanged;
    public IReadOnlyList<SopEvaluationApplicationRow> Applications { get; private set; } = [];
    public IReadOnlyList<SopEvaluationApplicationRow> FilteredApplications { get; private set; } = [];
    public string SearchText { get; private set; } = string.Empty;
    public string StatusFilter { get; private set; } = "all";
    public string? DatabaseError { get; private set; }
    public bool IsLoading { get; private set; } = true;
    public bool IsSaving { get; private set; }
    public int AssessedCount => _evaluations.Count(evaluation => evaluation.Level.HasValue);
    public int VerifiedCount => _evaluations.Count(evaluation => evaluation.Level == 4);
    public int NotAvailableCount => _evaluations.Count(evaluation => evaluation.Level == 0);
    public int UnassessedCount => Math.Max(
        0,
        Applications.Count * SopEvaluationCatalog.Categories.Count - AssessedCount);
    public decimal? OverallReadiness => AssessedCount == 0
        ? null
        : decimal.Round(
            (decimal)_evaluations.Where(evaluation => evaluation.Level.HasValue)
                .Sum(evaluation => evaluation.Level!.Value) /
            (AssessedCount * 4) * 100,
            0);

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
            _evaluations = await db.SopEvaluations.AsNoTracking()
                .Include(evaluation => evaluation.Documents)
                .ThenInclude(document => document.Children)
                .OrderBy(evaluation => evaluation.ApplicationName)
                .ThenBy(evaluation => evaluation.SopCategory)
                .ToListAsync();

            Applications = _evaluations
                .GroupBy(evaluation => evaluation.ApplicationName)
                .OrderBy(group => group.Key)
                .Select(group => new SopEvaluationApplicationRow(
                    group.Key,
                    SopEvaluationCatalog.Categories.ToDictionary(
                        category => category.Name,
                        category => group.FirstOrDefault(evaluation =>
                            evaluation.SopCategory == category.Name) ??
                            new SopEvaluation
                            {
                                ApplicationName = group.Key,
                                SopCategory = category.Name
                            })))
                .ToList();
            ApplyFilter();
        }
        catch (SqlException exception)
        {
            HandleError(exception, "memuat evaluasi SOP",
                "Database tidak dapat dihubungi. Periksa koneksi SQL Server, lalu coba lagi.");
        }
        catch (Exception exception)
        {
            HandleError(exception, "memuat evaluasi SOP",
                "Data SOP Evaluation gagal dimuat.");
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

    public void FilterByStatus(string? status)
    {
        StatusFilter = string.IsNullOrWhiteSpace(status) ? "all" : status;
        ApplyFilter();
        NotifyStateChanged();
    }

    public SopEvaluation GetAssessment(SopEvaluationApplicationRow application, string category) =>
        application.Assessments.TryGetValue(category, out var assessment)
            ? assessment
            : new SopEvaluation { ApplicationName = application.Name, SopCategory = category };

    public async Task CreateApplicationAsync(string applicationName)
    {
        var name = applicationName.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
        {
            snackbar.Add(string.IsNullOrWhiteSpace(name)
                ? "Nama aplikasi wajib diisi."
                : "Nama aplikasi maksimal 100 karakter.", Severity.Warning);
            return;
        }

        if (Applications.Any(application =>
                string.Equals(application.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            snackbar.Add("Aplikasi tersebut sudah terdaftar.", Severity.Warning);
            return;
        }

        IsSaving = true;
        NotifyStateChanged();
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            db.SopEvaluations.AddRange(SopEvaluationCatalog.Categories.Select(category =>
                new SopEvaluation
                {
                    ApplicationName = name,
                    SopCategory = category.Name
                }));
            await db.SaveChangesAsync();
            snackbar.Add("Aplikasi berhasil ditambahkan.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandleError(exception, "menambahkan aplikasi SOP",
                "Aplikasi gagal ditambahkan. Periksa data dan coba lagi.");
        }
        finally
        {
            IsSaving = false;
            NotifyStateChanged();
        }
    }

    public async Task SaveEvaluationAsync(SopEvaluationInput input)
    {
        if (input.Level is < 0 or > 4 ||
            !SopEvaluationCatalog.Categories.Any(category => category.Name == input.SopCategory))
        {
            snackbar.Add("Pilih level evaluasi SOP yang valid.", Severity.Warning);
            return;
        }

        if (input.OwnerName?.Length > 150 ||
            input.Documents.Any(document =>
                string.IsNullOrWhiteSpace(document.Title) ||
                document.Title.Trim().Length > 250 ||
                document.DocumentNumber?.Length > 100 ||
                document.Revision?.Length > 50 ||
                document.EvidenceUrl?.Length > 1000 ||
                document.Children.Any(child =>
                    child.DocumentType is not ("INK" or "STD") ||
                    string.IsNullOrWhiteSpace(child.Title) ||
                    child.Title.Trim().Length > 250 ||
                    child.DocumentNumber?.Length > 100 ||
                    child.Revision?.Length > 50 ||
                    child.EvidenceUrl?.Length > 1000)))
        {
            snackbar.Add(
                "Periksa judul dan panjang data dokumen, nomor, revisi, PIC, atau tautan bukti.",
                Severity.Warning);
            return;
        }

        IsSaving = true;
        NotifyStateChanged();
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var evaluation = await db.SopEvaluations
                .Include(item => item.Documents)
                .ThenInclude(document => document.Children)
                .SingleOrDefaultAsync(item =>
                    item.ApplicationName == input.ApplicationName &&
                    item.SopCategory == input.SopCategory);
            if (evaluation is null)
            {
                evaluation = new SopEvaluation
                {
                    ApplicationName = input.ApplicationName,
                    SopCategory = input.SopCategory
                };
                db.SopEvaluations.Add(evaluation);
            }

            evaluation.Level = input.Level;
            db.SopEvaluationDocuments.RemoveRange(evaluation.Documents);
            evaluation.Documents.Clear();
            foreach (var document in input.Documents)
            {
                evaluation.Documents.Add(new SopEvaluationDocument
                {
                    Title = document.Title.Trim(),
                    DocumentNumber = Normalize(document.DocumentNumber),
                    Revision = Normalize(document.Revision),
                    EvidenceUrl = Normalize(document.EvidenceUrl),
                    Children = document.Children.Select(child => new SopEvaluationChildDocument
                    {
                        DocumentType = child.DocumentType,
                        Title = child.Title.Trim(),
                        DocumentNumber = Normalize(child.DocumentNumber),
                        Revision = Normalize(child.Revision),
                        EvidenceUrl = Normalize(child.EvidenceUrl)
                    }).ToList()
                });
            }

            evaluation.DocumentTitle = null;
            evaluation.DocumentNumber = null;
            evaluation.Revision = null;
            evaluation.OwnerName = Normalize(input.OwnerName);
            evaluation.EvidenceUrl = null;
            evaluation.EvaluationDate = input.EvaluationDate?.Date;
            evaluation.Findings = Normalize(input.Findings);
            evaluation.FollowUpAction = Normalize(input.FollowUpAction);
            evaluation.FollowUpDueDate = input.FollowUpDueDate?.Date;
            evaluation.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            snackbar.Add("Evaluasi SOP berhasil disimpan.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandleError(exception, "menyimpan evaluasi SOP",
                "Evaluasi SOP gagal disimpan. Periksa data dan coba lagi.");
        }
        finally
        {
            IsSaving = false;
            NotifyStateChanged();
        }
    }

    private void ApplyFilter()
    {
        var filtered = Applications.Where(application =>
            application.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        if (StatusFilter == "unassessed")
        {
            filtered = filtered.Where(application =>
                application.Assessments.Values.Any(assessment => assessment.Level is null));
        }
        else if (int.TryParse(StatusFilter, out var level))
        {
            filtered = filtered.Where(application =>
                application.Assessments.Values.Any(assessment => assessment.Level == level));
        }

        FilteredApplications = filtered.ToList();
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

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void NotifyStateChanged() => StateChanged?.Invoke();
}

public sealed record SopEvaluationApplicationRow(
    string Name,
    IReadOnlyDictionary<string, SopEvaluation> Assessments)
{
    public int AssessedCount => Assessments.Values.Count(assessment => assessment.Level.HasValue);
    public int TotalScore => Assessments.Values.Sum(assessment => assessment.Level ?? 0);
    public decimal? Readiness => AssessedCount == 0
        ? null
        : decimal.Round((decimal)TotalScore / (AssessedCount * 4) * 100, 0);
}

public sealed record SopEvaluationInput(
    string ApplicationName,
    string SopCategory,
    int Level,
    IReadOnlyList<SopEvaluationDocumentInput> Documents,
    string? OwnerName,
    DateTime? EvaluationDate,
    string? Findings,
    string? FollowUpAction,
    DateTime? FollowUpDueDate);

public sealed record SopEvaluationDocumentInput(
    string Title,
    string? DocumentNumber,
    string? Revision,
    string? EvidenceUrl,
    IReadOnlyList<SopEvaluationChildDocumentInput> Children);

public sealed record SopEvaluationChildDocumentInput(
    string DocumentType,
    string Title,
    string? DocumentNumber,
    string? Revision,
    string? EvidenceUrl);
