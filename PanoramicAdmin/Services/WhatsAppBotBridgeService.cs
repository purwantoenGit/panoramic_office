using System.Net.Http.Json;
using PanoramicAdmin.Models;

namespace PanoramicAdmin.Services;

public sealed class WhatsAppBotBridgeService(
    HttpClient httpClient,
    IConfiguration configuration)
{
    public async Task SendMessageAsync(
        string phoneNumber,
        string message,
        CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["WhatsAppBot:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("WhatsAppBot:ApiKey belum dikonfigurasi.");
        }

        if (!MasterPicContact.TryNormalizePhoneNumber(phoneNumber, out var normalizedPhoneNumber) ||
            normalizedPhoneNumber is null)
        {
            throw new ArgumentException("Nomor WhatsApp PIC tidak valid.", nameof(phoneNumber));
        }

        var bridgeBaseUrl = configuration["WhatsAppBot:BridgeUrl"] ?? "http://127.0.0.1:3100";
        if (!Uri.TryCreate(bridgeBaseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttps &&
             (baseUri.Scheme != Uri.UriSchemeHttp || !baseUri.IsLoopback)))
        {
            throw new InvalidOperationException(
                "WhatsAppBot:BridgeUrl harus berupa URL HTTPS atau URL HTTP lokal.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri($"{baseUri.ToString().TrimEnd('/')}/api/send-message", UriKind.Absolute));
        request.Headers.Add("X-Bot-Api-Key", apiKey);
        request.Content = JsonContent.Create(new
        {
            phoneNumber = normalizedPhoneNumber,
            message
        });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"WhatsApp bot bridge mengembalikan HTTP {(int)response.StatusCode}.",
                null,
                response.StatusCode);
        }
    }
}
