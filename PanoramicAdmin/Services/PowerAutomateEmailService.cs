using System.Net.Http.Json;
using PanoramicAdmin.Models;

namespace PanoramicAdmin.Services;

public sealed class PowerAutomateEmailService(
    HttpClient httpClient,
    IConfiguration configuration)
{
    public async Task<bool> SendActionPlanEmailAsync(
        string recipientEmail,
        string subject,
        string bodyHtml,
        CancellationToken cancellationToken = default)
    {
        if (!MasterPicContact.TryNormalizeEmail(recipientEmail, out var normalizedEmail) ||
            normalizedEmail is null)
        {
            throw new ArgumentException("Alamat email PIC tidak valid.", nameof(recipientEmail));
        }

        var flowUrl = configuration["PowerAutomate:ActionPlanEmailFlowUrl"];
        if (!Uri.TryCreate(flowUrl, UriKind.Absolute, out var flowUri) ||
            flowUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "PowerAutomate:ActionPlanEmailFlowUrl belum dikonfigurasi dengan URL HTTPS yang valid.");
        }

        using var response = await httpClient.PostAsJsonAsync(
            flowUri,
            new
            {
                to = normalizedEmail,
                subject,
                bodyHtml
            },
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            return true;
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
        {
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Power Automate email flow mengembalikan HTTP {(int)response.StatusCode}.",
                null,
                response.StatusCode);
        }

        throw new HttpRequestException(
            $"Power Automate email flow mengembalikan status sukses yang tidak terduga: HTTP {(int)response.StatusCode}.",
            null,
            response.StatusCode);
    }
}
