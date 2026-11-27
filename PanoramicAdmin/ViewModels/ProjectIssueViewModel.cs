using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using PanoramicAdmin.Models;
using PanoramicAdmin.Services;

namespace PanoramicAdmin.ViewModels;

public sealed class ProjectIssueViewModel(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IConfiguration configuration,
    ISnackbar snackbar,
    ILogger<ProjectIssueViewModel> logger,
    WhatsAppBotBridgeService whatsAppBotBridgeService,
    PowerAutomateEmailService powerAutomateEmailService,
    ProjectIssueFollowUpService projectIssueFollowUpService)
{
    private readonly HashSet<(int ActionPlanId, int PicId)> _sendingWhatsAppMessages = [];
    private readonly HashSet<(int ActionPlanId, int PicId)> _sendingActionPlanEmails = [];

    public event Action? StateChanged;
    public IReadOnlyList<ProjectIssueRow> Issues { get; private set; } = [];
    public IReadOnlyList<ProjectIssueRow> FilteredIssues { get; private set; } = [];
    public IReadOnlyList<ReferenceOption> Projects { get; private set; } = [];
    public IReadOnlyList<ReferenceOption> Pics { get; private set; } = [];
    public IReadOnlyList<ReferenceOption> StrategicActions { get; private set; } = [];
    public IReadOnlyList<string> ProjectStatuses { get; private set; } = [];
    public string SearchText { get; private set; } = string.Empty;
    public string ProjectNameFilter { get; private set; } = string.Empty;
    public int? SelectedProjectId { get; private set; }
    public string? DatabaseError { get; private set; }
    public int? PendingDeleteId { get; private set; }
    public bool IsLoading { get; private set; } = true;
    public bool IsSaving { get; private set; }
    public bool IsSendingWhatsApp(int actionPlanId, int picId) =>
        _sendingWhatsAppMessages.Contains((actionPlanId, picId));
    public bool IsSendingActionPlanEmail(int actionPlanId, int picId) =>
        _sendingActionPlanEmails.Contains((actionPlanId, picId));

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
            Projects = await db.Projects.AsNoTracking()
                .OrderBy(project => project.Name)
                .Select(project => new ReferenceOption(project.Id, project.Name))
                .ToListAsync();
            Pics = await db.MasterPics.AsNoTracking()
                .OrderBy(pic => pic.Nama)
                .Select(pic => new ReferenceOption(pic.Id, pic.Nama, pic.PhoneNumber, pic.Email))
                .ToListAsync();
            StrategicActions = await db.StrategicActions.AsNoTracking()
                .OrderBy(action => action.NamaStrategicAction)
                .Select(action => new ReferenceOption(action.Id, action.NamaStrategicAction))
                .ToListAsync();
            ProjectStatuses = await db.MasterIssues.AsNoTracking()
                .Where(issue => issue.Active)
                .OrderBy(issue => issue.Issue)
                .Select(issue => issue.Issue)
                .ToListAsync();

            var issues = await db.ProjectIssues.AsNoTracking()
                .Include(issue => issue.Project)
                .Include(issue => issue.StrategicAction)
                .Include(issue => issue.ActionPlans)
                    .ThenInclude(plan => plan.Pic)
                .Include(issue => issue.ActionPlans)
                    .ThenInclude(plan => plan.IssuerPic)
                .Include(issue => issue.ActionPlans)
                    .ThenInclude(plan => plan.PicAssignments)
                        .ThenInclude(assignment => assignment.Pic)
                .OrderBy(issue => issue.Id)
                .ToListAsync();
            Issues = issues.Select((issue, index) => new ProjectIssueRow(
                issue.Id,
                index + 1,
                issue.ProjectId,
                issue.Project?.Name ?? "(Project tidak tersedia)",
                issue.StatusProject,
                issue.Tanggal,
                issue.StrategicActionId,
                issue.StrategicAction?.NamaStrategicAction ?? "(Strategic Action tidak tersedia)",
                issue.ActionPlans.Select(plan =>
                {
                    var assignedPics = plan.PicAssignments
                        .Select(assignment => assignment.Pic)
                        .OfType<MasterPic>()
                        .Select(pic => new ReferenceOption(pic.Id, pic.Nama, pic.PhoneNumber, pic.Email))
                        .ToList();
                    if (assignedPics.Count == 0 && plan.Pic is not null)
                    {
                        assignedPics.Add(new ReferenceOption(
                            plan.Pic.Id, plan.Pic.Nama, plan.Pic.PhoneNumber, plan.Pic.Email));
                    }

                    return new ProjectIssueActionPlanRow(
                        plan.Id,
                        plan.IssuerPicId,
                        new ReferenceOption(
                            plan.IssuerPic?.Id ?? 0,
                            plan.IssuerPic?.Nama ?? "(PIC tidak tersedia)"),
                        plan.ActionPlan,
                        plan.Status,
                        assignedPics.Select(pic => pic.Id).ToArray(),
                        assignedPics,
                        plan.Feedback);
                }).ToList(),
                issue.DueDate)).ToList();
            ApplyFilter();
        }
        catch (Exception exception)
        {
            HandleError(exception, "memuat Project Issue", "Data Project Issue gagal dimuat.");
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

    public void FilterByProject(int? projectId)
    {
        SelectedProjectId = projectId;
        ApplyFilter();
        NotifyStateChanged();
    }

    public void FilterByProjectName(string? projectName)
    {
        ProjectNameFilter = projectName ?? string.Empty;
        ApplyFilter();
        NotifyStateChanged();
    }

    private void ApplyFilter()
    {
        FilteredIssues = Issues
            .Where(issue =>
                (SelectedProjectId is null || issue.ProjectId == SelectedProjectId.Value) &&
                (string.IsNullOrWhiteSpace(ProjectNameFilter) ||
                 issue.Project.Contains(ProjectNameFilter.Trim(), StringComparison.OrdinalIgnoreCase)) &&
                (issue.Project.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                 issue.StatusProject.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                 issue.StrategicAction.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                 issue.ActionPlans.Any(plan =>
                     plan.IssuerPic.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                     plan.ActionPlan.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                     plan.Status.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                     plan.Pics.Any(pic => pic.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                     (plan.Feedback?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)) ||
                 issue.Tanggal.ToString("dd/MM/yyyy").Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                 (issue.DueDate?.ToString("dd/MM/yyyy").Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)))
            .Select((issue, index) => issue with { No = index + 1 })
            .ToList();
    }

    public async Task SendActionPlanWhatsAppAsync(
        ProjectIssueRow issue,
        ProjectIssueActionPlanRow plan,
        ReferenceOption pic)
    {
        if (!plan.PicIds.Contains(pic.Id))
        {
            snackbar.Add("PIC tersebut tidak ditugaskan pada Action Plan ini.", Severity.Warning);
            return;
        }

        if (!MasterPicContact.TryNormalizePhoneNumber(pic.PhoneNumber, out var phoneNumber) ||
            phoneNumber is null)
        {
            snackbar.Add("Nomor WhatsApp PIC belum diisi atau tidak valid di Master PIC.", Severity.Warning);
            return;
        }

        var requestKey = (plan.Id, pic.Id);
        if (!_sendingWhatsAppMessages.Add(requestKey))
        {
            return;
        }

        NotifyStateChanged();
        try
        {
            var dueDate = issue.DueDate is { } date
               ? $"📅 *Due Date:* *{date:dd/MM/yyyy}*\n"
               : string.Empty;
            var message =
               $"📣 *ACTION PLAN - MOHON TINDAK LANJUT*\n\n" +
               $"👋 Halo *{pic.Name}*, mohon tindak lanjut Action Plan berikut:\n\n" +
               $"🏢 *Project:* {issue.Project}\n" +
               $"🚩 *Project Issue:* {issue.StatusProject}\n" +
               $"🎯 *Strategic Action:* {issue.StrategicAction}\n" +
               $"👤 *Issuer:* {plan.IssuerPic.Name}\n" +
               $"📝 *Action Plan:*\n*{plan.ActionPlan}*\n" +
               $"🔄 *Status:* *{plan.Status}*\n" +
               dueDate;

            await whatsAppBotBridgeService.SendMessageAsync(phoneNumber, message);
            snackbar.Add($"Action Plan berhasil dikirim ke {pic.Name} melalui WhatsApp.", Severity.Success);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(
               exception,
               "WhatsApp send failed for Project Issue {IssueId}, Action Plan {ActionPlanId}, PIC {PicId}.",
               issue.Id,
               plan.Id,
               pic.Id);
            snackbar.Add(
               "WhatsApp gagal mengirim. Pastikan bot aktif, terhubung, dan nomor PIC terdaftar di WhatsApp.",
               Severity.Error);
        }
        catch (TaskCanceledException exception)
        {
            logger.LogError(
               exception,
               "WhatsApp send timed out for Project Issue {IssueId}, Action Plan {ActionPlanId}, PIC {PicId}.",
               issue.Id,
               plan.Id,
               pic.Id);
            snackbar.Add("Pengiriman WhatsApp timeout. Periksa koneksi bot lalu coba lagi.", Severity.Error);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "WhatsApp bot bridge configuration is invalid.");
            snackbar.Add("Konfigurasi WhatsApp bot belum lengkap. Hubungi admin.", Severity.Error);
        }
        finally
        {
            _sendingWhatsAppMessages.Remove(requestKey);
            NotifyStateChanged();
        }
    }

    public async Task SendActionPlanEmailAsync(
        ProjectIssueRow issue,
        ProjectIssueActionPlanRow plan,
        ReferenceOption pic)
    {
        if (!plan.PicIds.Contains(pic.Id))
        {
            snackbar.Add("PIC tersebut tidak ditugaskan pada Action Plan ini.", Severity.Warning);
            return;
        }

        if (!MasterPicContact.TryNormalizeEmail(pic.Email, out var email) || email is null)
        {
            snackbar.Add("Alamat email PIC belum diisi atau tidak valid di Master PIC.", Severity.Warning);
            return;
        }

        var requestKey = (plan.Id, pic.Id);
        if (!_sendingActionPlanEmails.Add(requestKey))
        {
            return;
        }

        NotifyStateChanged();
        try
        {
            var subjectProject = issue.Project.Replace('\r', ' ').Replace('\n', ' ').Trim();
            var subject = $"Action Plan - {subjectProject}";
            var followUpUrl = await projectIssueFollowUpService.CreateLinkAsync(plan.Id, pic.Id);
            var bodyHtml = BuildActionPlanEmailHtml(issue, plan, pic, followUpUrl);

            var deliveryConfirmed =
                await powerAutomateEmailService.SendActionPlanEmailAsync(email, subject, bodyHtml);
            snackbar.Add(
                deliveryConfirmed
                    ? $"Action Plan berhasil dikirim ke {pic.Name} melalui email."
                    : $"Power Automate menerima permintaan email untuk {pic.Name}. Periksa riwayat flow untuk memastikan terkirim.",
                Severity.Success);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(
                "Power Automate email failed for Project Issue {IssueId}, Action Plan {ActionPlanId}, " +
                "PIC {PicId}; HTTP status {StatusCode}.",
                issue.Id,
                plan.Id,
                pic.Id,
                exception.StatusCode);
            snackbar.Add(
                "Email gagal dikirim. Periksa konfigurasi dan riwayat flow Power Automate.",
                Severity.Error);
        }
        catch (TaskCanceledException)
        {
            logger.LogError(
                "Power Automate email timed out for Project Issue {IssueId}, Action Plan {ActionPlanId}, PIC {PicId}.",
                issue.Id,
                plan.Id,
                pic.Id);
            snackbar.Add("Pengiriman email timeout. Periksa flow Power Automate lalu coba lagi.", Severity.Error);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Action Plan email or follow-up link configuration is invalid.");
            snackbar.Add(
                "Periksa konfigurasi flow Power Automate dan alamat HTTPS PanoramicReview untuk follow-up.",
                Severity.Error);
        }
        catch (SqlException exception)
        {
            logger.LogError(
                exception,
                "Unable to create Action Plan follow-up link for Project Issue {IssueId}, Action Plan {ActionPlanId}.",
                issue.Id,
                plan.Id);
            snackbar.Add(
                "Link follow-up gagal dibuat. Pastikan skrip database Project Issue Follow-up sudah dijalankan.",
                Severity.Error);
        }
        finally
        {
            _sendingActionPlanEmails.Remove(requestKey);
            NotifyStateChanged();
        }
    }

    private static string BuildActionPlanEmailHtml(
        ProjectIssueRow issue,
        ProjectIssueActionPlanRow plan,
        ReferenceOption pic,
        string followUpUrl)
    {
        var dueDateRow = issue.DueDate is { } date
            ? $"""
               <tr>
                 <td style="padding:10px 12px;color:#526174;white-space:nowrap;">📅 Due Date</td>
                 <td style="padding:10px 12px;"><strong>{date:dd/MM/yyyy}</strong></td>
               </tr>
               """
            : string.Empty;

        return $"""
                <!doctype html>
                <html>
                <body style="margin:0;padding:24px;background:#f4f7fb;font-family:Segoe UI,Arial,sans-serif;color:#243247;">
                  <div style="max-width:680px;margin:0 auto;padding:24px;background:#ffffff;border:1px solid #e5eaf2;border-radius:12px;">
                    <p style="margin:0 0 8px;color:#718096;font-size:12px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;">Pesan Otomatis dari PANORAMIC REVIEW!</p>
                    <h2 style="margin:0 0 18px;color:#2457a6;font-size:20px;">📣 Action Plan - Mohon Tindak Lanjut</h2>
                    <p style="margin:0 0 20px;line-height:1.6;">👋 Halo <strong>{EncodeHtml(pic.Name)}</strong>, mohon tindak lanjut Action Plan berikut:</p>
                    <table role="presentation" style="width:100%;border-collapse:collapse;font-size:14px;">
                      <tr>
                        <td style="padding:10px 12px;color:#526174;white-space:nowrap;">🏢 Project</td>
                        <td style="padding:10px 12px;">{EncodeHtml(issue.Project)}</td>
                      </tr>
                      <tr style="background:#f8fafd;">
                        <td style="padding:10px 12px;color:#526174;white-space:nowrap;">🚩 Project Issue</td>
                        <td style="padding:10px 12px;">{EncodeHtml(issue.StatusProject)}</td>
                      </tr>
                      <tr>
                        <td style="padding:10px 12px;color:#526174;white-space:nowrap;">🎯 Strategic Action</td>
                        <td style="padding:10px 12px;">{EncodeHtml(issue.StrategicAction)}</td>
                      </tr>
                      <tr style="background:#f8fafd;">
                        <td style="padding:10px 12px;color:#526174;white-space:nowrap;">👤 Issuer</td>
                        <td style="padding:10px 12px;">{EncodeHtml(plan.IssuerPic.Name)}</td>
                      </tr>
                      <tr>
                        <td style="padding:10px 12px;color:#526174;white-space:nowrap;vertical-align:top;">📝 Action Plan</td>
                        <td style="padding:10px 12px;line-height:1.6;"><strong>{EncodeHtmlWithLineBreaks(plan.ActionPlan)}</strong></td>
                      </tr>
                      <tr style="background:#f8fafd;">
                        <td style="padding:10px 12px;color:#526174;white-space:nowrap;">🔄 Status</td>
                        <td style="padding:10px 12px;"><strong>{EncodeHtml(plan.Status)}</strong></td>
                      </tr>
                      {dueDateRow}
                    </table>
                    <div style="margin-top:24px;text-align:center;">
                      <a href="{EncodeHtml(followUpUrl)}" style="display:inline-block;padding:12px 20px;border-radius:8px;background:#2457a6;color:#ffffff;text-decoration:none;font-weight:700;">🔗 Link to Follow Up</a>
                    </div>
                  </div>
                </body>
                </html>
                """;
    }

    private static string EncodeHtml(string value) =>
        System.Net.WebUtility.HtmlEncode(value);

    private static string EncodeHtmlWithLineBreaks(string value) =>
        EncodeHtml(value)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Replace("\n", "<br>");

    public async Task CreateAsync(ProjectIssueInput input) => await SaveAsync(null, input);
    public async Task UpdateAsync(int id, ProjectIssueInput input) => await SaveAsync(id, input);

    private async Task SaveAsync(int? id, ProjectIssueInput input)
    {
        var status = input.StatusProject.Trim();
        var strategicActionName = input.StrategicActionName.Trim();
        var actionPlans = input.ActionPlans.Select(plan => new ProjectIssueActionPlanInput(
            plan.Id,
            plan.IssuerPicId,
            plan.ActionPlan.Trim(),
            plan.Status.Trim(),
            plan.PicIds.Distinct().ToArray(),
            string.IsNullOrWhiteSpace(plan.Feedback) ? null : plan.Feedback.Trim())).ToList();
        if (!Validate(input, status, actionPlans)) return;

        IsSaving = true;
        NotifyStateChanged();
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var projectExists = await db.Projects.AnyAsync(project => project.Id == input.ProjectId);
            var issuerPicIds = actionPlans.Select(plan => plan.IssuerPicId).Distinct().ToArray();
            var validIssuerPicCount = await db.MasterPics.CountAsync(pic => issuerPicIds.Contains(pic.Id));
            var actionPlanPicIds = actionPlans.SelectMany(plan => plan.PicIds).Distinct().ToArray();
            var validActionPlanPicCount = await db.MasterPics.CountAsync(pic => actionPlanPicIds.Contains(pic.Id));
            if (!projectExists || validIssuerPicCount != issuerPicIds.Length ||
                validActionPlanPicCount != actionPlanPicIds.Length)
            {
                snackbar.Add("Pilihan Project, Issuer, atau PIC Action Plan sudah berubah. Muat ulang data dan pilih kembali.", Severity.Warning);
                await LoadAsync();
                return;
            }

            var strategicAction = await db.StrategicActions
                .FirstOrDefaultAsync(action => action.NamaStrategicAction == strategicActionName);
            var isNewStrategicAction = strategicAction is null;
            if (strategicAction is null)
            {
                strategicAction = new StrategicAction { NamaStrategicAction = strategicActionName };
                db.StrategicActions.Add(strategicAction);
            }

            var activeStatuses = await db.MasterIssues.AsNoTracking()
                .Where(masterIssue => masterIssue.Active)
                .Select(masterIssue => masterIssue.Issue)
                .ToListAsync();
            var activeStatusSet = activeStatuses.ToHashSet(StringComparer.Ordinal);
            var statusIsActive = activeStatusSet.Contains(status);
            var isUnchangedStatus = !statusIsActive && id is not null &&
                await db.ProjectIssues.AnyAsync(existingIssue =>
                    existingIssue.Id == id && existingIssue.StatusProject == status);
            if (!statusIsActive && !isUnchangedStatus)
            {
                snackbar.Add("Status Project sudah tidak aktif. Muat ulang data dan pilih Status Project yang aktif.", Severity.Warning);
                await LoadAsync();
                return;
            }

            ProjectIssue issue;
            if (id is null)
            {
                issue = new ProjectIssue { StatusProject = status };
                db.ProjectIssues.Add(issue);
            }
            else
            {
                issue = await db.ProjectIssues
                    .Include(item => item.ActionPlans)
                        .ThenInclude(plan => plan.PicAssignments)
                    .SingleOrDefaultAsync(item => item.Id == id)
                    ?? throw new InvalidOperationException("Project Issue tidak ditemukan.");
            }

            var existingActionPlans = issue.ActionPlans.ToDictionary(plan => plan.Id);
            var submittedIds = actionPlans
                .Where(plan => plan.Id.HasValue)
                .Select(plan => plan.Id!.Value)
                .ToArray();
            if (submittedIds.Distinct().Count() != submittedIds.Length ||
                (id is null && submittedIds.Length > 0) ||
                (id is not null && submittedIds.Any(planId => !existingActionPlans.ContainsKey(planId))))
            {
                snackbar.Add("Daftar Action Plan berubah. Muat ulang issue dan coba lagi.", Severity.Warning);
                await LoadAsync();
                return;
            }

            var invalidActionPlanStatus = actionPlans.Any(plan =>
                !activeStatusSet.Contains(plan.Status) &&
                (plan.Id is not int planId ||
                 !existingActionPlans.TryGetValue(planId, out var existingPlan) ||
                 !string.Equals(existingPlan.Status, plan.Status, StringComparison.Ordinal)));
            if (invalidActionPlanStatus)
            {
                snackbar.Add("Status Action Plan tidak aktif. Pilih status aktif dari Master Issue.", Severity.Warning);
                await LoadAsync();
                return;
            }

            issue.ProjectId = input.ProjectId;
            issue.StatusProject = status;
            issue.Tanggal = input.Tanggal.Date;
            issue.StrategicAction = strategicAction;
            issue.DueDate = input.DueDate?.Date;
            var retainedActionPlanIds = new HashSet<int>();
            foreach (var actionPlan in actionPlans)
            {
                ProjectIssueActionPlan actionPlanEntity;
                if (actionPlan.Id is int actionPlanId)
                {
                    actionPlanEntity = existingActionPlans[actionPlanId];
                    retainedActionPlanIds.Add(actionPlanId);
                }
                else
                {
                    actionPlanEntity = new ProjectIssueActionPlan
                    {
                        IssuerPicId = actionPlan.IssuerPicId,
                        ActionPlan = actionPlan.ActionPlan,
                        Status = actionPlan.Status,
                        PicId = actionPlan.PicIds[0],
                        PicAssignments = actionPlan.PicIds.Select(picId => new ProjectIssueActionPlanPic
                        {
                            PicId = picId
                        }).ToList(),
                        Feedback = actionPlan.Feedback
                    };
                    issue.ActionPlans.Add(actionPlanEntity);
                    continue;
                }

                actionPlanEntity.IssuerPicId = actionPlan.IssuerPicId;
                actionPlanEntity.ActionPlan = actionPlan.ActionPlan;
                actionPlanEntity.Status = actionPlan.Status;
                actionPlanEntity.PicId = actionPlan.PicIds[0];
                db.ProjectIssueActionPlanPics.RemoveRange(actionPlanEntity.PicAssignments);
                actionPlanEntity.PicAssignments.Clear();
                foreach (var picId in actionPlan.PicIds)
                {
                    actionPlanEntity.PicAssignments.Add(new ProjectIssueActionPlanPic
                    {
                        PicId = picId
                    });
                }
                actionPlanEntity.Feedback = actionPlan.Feedback;
            }
            foreach (var removedPlan in issue.ActionPlans
                         .Where(plan => plan.Id != 0 && !retainedActionPlanIds.Contains(plan.Id))
                         .ToList())
            {
                issue.ActionPlans.Remove(removedPlan);
                db.ProjectIssueActionPlans.Remove(removedPlan);
            }

            await db.SaveChangesAsync();
            var successMessage = id is null
                ? "Project Issue berhasil ditambahkan."
                : "Perubahan Project Issue berhasil disimpan.";
            if (isNewStrategicAction)
            {
                successMessage += " Strategic Action baru juga ditambahkan.";
            }
            snackbar.Add(successMessage, Severity.Success);
            SearchText = string.Empty;
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandleError(exception, id is null ? "menambahkan Project Issue" : "mengubah Project Issue",
                "Project Issue gagal disimpan. Periksa data dan coba lagi.");
        }
        finally
        {
            IsSaving = false;
            NotifyStateChanged();
        }
    }

    private bool Validate(
        ProjectIssueInput input,
        string status,
        IReadOnlyList<ProjectIssueActionPlanInput> actionPlans)
    {
        if (input.ProjectId <= 0 ||
            string.IsNullOrWhiteSpace(input.StrategicActionName) ||
            input.StrategicActionName.Trim().Length > 200)
        {
            snackbar.Add("Pilih Project, lalu pilih atau isi Strategic Action (maksimal 200 karakter).", Severity.Warning);
            return false;
        }
        if (string.IsNullOrWhiteSpace(status) || status.Length > 200)
        {
            snackbar.Add("Status Project wajib diisi dan maksimal 200 karakter.", Severity.Warning);
            return false;
        }
        if (actionPlans.Count == 0 ||
            actionPlans.Any(plan =>
                string.IsNullOrWhiteSpace(plan.ActionPlan) ||
                plan.IssuerPicId <= 0 ||
                string.IsNullOrWhiteSpace(plan.Status) ||
                plan.PicIds.Count == 0 ||
                plan.PicIds.Any(picId => picId <= 0)))
        {
            snackbar.Add("Tambahkan minimal satu Action Plan dan pilih Issuer, Status, serta minimal satu PIC untuk setiap Action Plan.", Severity.Warning);
            return false;
        }
        if (input.Tanggal == default)
        {
            snackbar.Add("Tanggal wajib diisi.", Severity.Warning);
            return false;
        }
        return true;
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
            var issue = await db.ProjectIssues.SingleOrDefaultAsync(item => item.Id == id);
            if (issue is null)
            {
                snackbar.Add("Project Issue tidak ditemukan. Daftar telah diperbarui.", Severity.Warning);
            }
            else
            {
                db.ProjectIssues.Remove(issue);
                await db.SaveChangesAsync();
                snackbar.Add("Project Issue berhasil dihapus.", Severity.Success);
            }
            PendingDeleteId = null;
            await LoadAsync();
        }
        catch (Exception exception)
        {
            HandleError(exception, "menghapus Project Issue", "Project Issue gagal dihapus. Coba lagi.");
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
            DatabaseError = "Database tidak dapat dihubungi. Periksa koneksi SQL Server dan pastikan skrip tabel telah dijalankan.";
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

public sealed record ReferenceOption(
    int Id,
    string Name,
    string? PhoneNumber = null,
    string? Email = null);

public sealed record ProjectIssueRow(
    int Id,
    int No,
    int ProjectId,
    string Project,
    string StatusProject,
    DateTime Tanggal,
    int StrategicActionId,
    string StrategicAction,
    IReadOnlyList<ProjectIssueActionPlanRow> ActionPlans,
    DateTime? DueDate);

public sealed record ProjectIssueInput(
    int ProjectId,
    string StatusProject,
    DateTime Tanggal,
    string StrategicActionName,
    IReadOnlyList<ProjectIssueActionPlanInput> ActionPlans,
    DateTime? DueDate);

public sealed record ProjectIssueActionPlanRow(
    int Id,
    int IssuerPicId,
    ReferenceOption IssuerPic,
    string ActionPlan,
    string Status,
    IReadOnlyList<int> PicIds,
    IReadOnlyList<ReferenceOption> Pics,
    string? Feedback);

public sealed record ProjectIssueActionPlanInput(
    int? Id,
    int IssuerPicId,
    string ActionPlan,
    string Status,
    IReadOnlyList<int> PicIds,
    string? Feedback);
