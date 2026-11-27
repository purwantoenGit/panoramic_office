using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PanoramicAdmin.Models;

namespace PanoramicAdmin.Services;

public sealed class ProjectIssueFollowUpService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IConfiguration configuration)
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(30);

    public async Task<string> CreateLinkAsync(
        int actionPlanId,
        int picId,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = configuration["ProjectIssueFollowUp:BaseUrl"];
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            !string.IsNullOrEmpty(baseUri.UserInfo) ||
            !string.IsNullOrEmpty(baseUri.Query) ||
            !string.IsNullOrEmpty(baseUri.Fragment))
        {
            throw new InvalidOperationException("ProjectIssueFollowUp:BaseUrl bukan alamat aplikasi yang valid.");
        }

        if (baseUri.Scheme == Uri.UriSchemeHttp)
        {
            if (!bool.TryParse(configuration["ProjectIssueFollowUp:AllowInsecureHttp"], out var allowInsecureHttp) ||
                !allowInsecureHttp ||
                !await IsPrivateNetworkHostAsync(baseUri.DnsSafeHost, cancellationToken))
            {
                throw new InvalidOperationException(
                    "HTTP hanya diizinkan jika ProjectIssueFollowUp:AllowInsecureHttp=true " +
                    "dan alamat server berada di jaringan privat.");
            }
        }
        else if (baseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "ProjectIssueFollowUp:BaseUrl harus menggunakan HTTPS atau HTTP privat yang diizinkan.");
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var isAssigned = await db.ProjectIssueActionPlanPics.AnyAsync(
            assignment => assignment.ProjectIssueActionPlanId == actionPlanId &&
                          assignment.PicId == picId,
            cancellationToken) ||
                         await db.ProjectIssueActionPlans.AnyAsync(
                             plan => plan.Id == actionPlanId && plan.PicId == picId,
                             cancellationToken);
        if (!isAssigned)
        {
            throw new InvalidOperationException("PIC tidak ditugaskan pada Action Plan ini.");
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var now = DateTime.UtcNow;
        await db.ProjectIssueActionPlanFollowUpTokens
            .Where(item => item.ExpiresAtUtc <= now)
            .ExecuteDeleteAsync(cancellationToken);
        db.ProjectIssueActionPlanFollowUpTokens.Add(new ProjectIssueActionPlanFollowUpToken
        {
            TokenHash = HashToken(token),
            ActionPlanId = actionPlanId,
            PicId = picId,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(TokenLifetime)
        });
        await db.SaveChangesAsync(cancellationToken);

        var appRoot = new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
        var followUpUri = new Uri(appRoot, "project-issue/follow-up");
        return $"{followUpUri.AbsoluteUri}#token={token}";
    }

    public async Task<ProjectIssueFollowUpPageData?> GetPageDataAsync(
        string? token,
        CancellationToken cancellationToken = default)
    {
        if (!TryHashToken(token, out var tokenHash))
        {
            return null;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var entry = await db.ProjectIssueActionPlanFollowUpTokens.AsNoTracking()
            .Where(item => item.TokenHash == tokenHash &&
                           item.ExpiresAtUtc > now)
            .Select(item => new
            {
                item.ActionPlan!.Feedback,
                item.ActionPlan!.Status,
                item.ActionPlan!.ActionPlan,
                Issuer = item.ActionPlan!.IssuerPic!.Nama,
                Pic = item.Pic!.Nama,
                Project = item.ActionPlan!.ProjectIssue!.Project!.Name,
                ProjectIssue = item.ActionPlan!.ProjectIssue!.StatusProject,
                StrategicAction = item.ActionPlan!.ProjectIssue!.StrategicAction!.NamaStrategicAction,
                DueDate = item.ActionPlan!.ProjectIssue!.DueDate
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (entry is null)
        {
            return null;
        }

        var statuses = await db.MasterIssues.AsNoTracking()
            .Where(issue => issue.Active)
            .OrderBy(issue => issue.Issue)
            .Select(issue => issue.Issue)
            .ToListAsync(cancellationToken);

        return new ProjectIssueFollowUpPageData(
            entry.Project,
            entry.ProjectIssue,
            entry.StrategicAction,
            entry.Issuer,
            entry.Pic,
            entry.ActionPlan,
            entry.Status,
            entry.Feedback,
            entry.DueDate,
            statuses);
    }

    public async Task<FollowUpSaveResult> SaveAsync(
        string? token,
        string? feedback,
        string? newStatus,
        CancellationToken cancellationToken = default)
    {
        if (!TryHashToken(token, out var tokenHash))
        {
            return FollowUpSaveResult.InvalidOrExpired;
        }

        var cleanedFeedback = feedback?.Trim();
        if (string.IsNullOrWhiteSpace(cleanedFeedback))
        {
            return FollowUpSaveResult.FeedbackRequired;
        }

        if (string.IsNullOrWhiteSpace(newStatus))
        {
            return FollowUpSaveResult.StatusRequired;
        }

        await using var strategyContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var executionStrategy = strategyContext.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var now = DateTime.UtcNow;
            var tokenRecord = await db.ProjectIssueActionPlanFollowUpTokens
                .SingleOrDefaultAsync(
                    item => item.TokenHash == tokenHash &&
                            item.ExpiresAtUtc > now,
                    cancellationToken);
            if (tokenRecord is null)
            {
                return FollowUpSaveResult.InvalidOrExpired;
            }

            var plan = await db.ProjectIssueActionPlans
                .SingleOrDefaultAsync(item => item.Id == tokenRecord.ActionPlanId, cancellationToken);
            if (plan is null)
            {
                return FollowUpSaveResult.InvalidOrExpired;
            }

            var picStillAssigned = await db.ProjectIssueActionPlanPics.AnyAsync(
                assignment => assignment.ProjectIssueActionPlanId == plan.Id &&
                              assignment.PicId == tokenRecord.PicId,
                cancellationToken) ||
                                  plan.PicId == tokenRecord.PicId;
            if (!picStillAssigned)
            {
                return FollowUpSaveResult.InvalidOrExpired;
            }

            if (string.Equals(plan.Status, newStatus.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return FollowUpSaveResult.StatusMustChange;
            }

            if (!await db.MasterIssues.AnyAsync(
                    issue => issue.Active && issue.Issue == newStatus.Trim(),
                    cancellationToken))
            {
                return FollowUpSaveResult.InvalidStatus;
            }

            plan.Feedback = string.IsNullOrWhiteSpace(plan.Feedback)
                ? cleanedFeedback
                : $"{plan.Feedback.Trim()}\n\nFollow-up ({now:yyyy-MM-dd HH:mm} UTC):\n{cleanedFeedback}";
            plan.Status = newStatus.Trim();
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return FollowUpSaveResult.Saved;
        });
    }

    private static bool TryHashToken(string? token, out string tokenHash)
    {
        tokenHash = string.Empty;
        if (string.IsNullOrWhiteSpace(token) ||
            token.Length != 43 ||
            token.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_')))
        {
            return false;
        }

        tokenHash = HashToken(token);
        return true;
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static async Task<bool> IsPrivateNetworkHostAsync(
        string host,
        CancellationToken cancellationToken)
    {
        IPAddress[] addresses;
        try
        {
            addresses = IPAddress.TryParse(host, out var address)
                ? [address]
                : await Dns.GetHostAddressesAsync(host, cancellationToken);
        }
        catch (SocketException exception)
        {
            throw new InvalidOperationException(
                "ProjectIssueFollowUp:BaseUrl host tidak dapat di-resolve ke jaringan privat.",
                exception);
        }

        return addresses.Length > 0 && addresses.All(IsPrivateNetworkAddress);
    }

    private static bool IsPrivateNetworkAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.IPv6Any) || address.IsIPv6LinkLocal || address.IsIPv6Multicast)
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] == 10 ||
                   (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168);
        }

        return address.AddressFamily == AddressFamily.InterNetworkV6 &&
               (bytes[0] & 0xFE) == 0xFC;
    }
}

public sealed record ProjectIssueFollowUpPageData(
    string Project,
    string ProjectIssue,
    string StrategicAction,
    string Issuer,
    string Pic,
    string ActionPlan,
    string CurrentStatus,
    string? ExistingFeedback,
    DateTime? DueDate,
    IReadOnlyList<string> AvailableStatuses);

public enum FollowUpSaveResult
{
    Saved,
    InvalidOrExpired,
    FeedbackRequired,
    StatusRequired,
    StatusMustChange,
    InvalidStatus
}
