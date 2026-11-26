using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.FileProviders;
using MudBlazor.Services;
using System.Security.Cryptography;
using System.Text;
using PanoramicAdmin.Components;
using PanoramicAdmin.Models;
using PanoramicAdmin.Services;
using PanoramicAdmin.ViewModels;

var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment())
{
    var envPath = Path.Combine(builder.Environment.ContentRootPath, ".env");
    if (File.Exists(envPath))
    {
        DotNetEnv.Env.Load(envPath);
        builder.Configuration.AddEnvironmentVariables();
    }
}

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();
var connectionStringBuilder = new SqlConnectionStringBuilder(
    builder.Configuration.GetConnectionString("PanoramicDb"));
if (builder.Environment.IsDevelopment())
{
    connectionStringBuilder.TrustServerCertificate = true;
}

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlServer(
        connectionStringBuilder.ConnectionString,
        sqlServerOptions => sqlServerOptions.EnableRetryOnFailure()));
builder.Services.AddOptions<OllamaOptions>()
    .Bind(builder.Configuration.GetSection("Ollama"))
    .Validate(options =>
            Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) &&
            (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)),
        "Ollama:BaseUrl must be an absolute HTTP or HTTPS URL.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Model),
        "Ollama:Model must not be empty.")
    .ValidateOnStart();
builder.Services.AddHttpClient<ScrumAssistantService>(client =>
    client.Timeout = TimeSpan.FromMinutes(2));
builder.Services.AddHttpClient<WhatsAppBotBridgeService>(client =>
    client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddSingleton<IUserService, InMemoryUserService>();
builder.Services.AddTransient<UserListViewModel>();
builder.Services.AddTransient<MasterProjectViewModel>();
builder.Services.AddTransient<MasterCategoryProjectViewModel>();
builder.Services.AddTransient<MasterStreamViewModel>();
builder.Services.AddTransient<MasterSitesViewModel>();
builder.Services.AddTransient<MasterPicViewModel>();
builder.Services.AddTransient<MasterRolesViewModel>();
builder.Services.AddTransient<MasterIssueViewModel>();
builder.Services.AddTransient<VirtualOfficeViewModel>();
builder.Services.AddTransient<StrategicActionViewModel>();
builder.Services.AddTransient<ProjectIssueViewModel>();
builder.Services.AddTransient<RfidEquipmentService>();
builder.Services.AddTransient<RfidManualUnitService>();
builder.Services.AddTransient<RfidTagInstallViewModel>();
builder.Services.AddTransient<PitStopMappingService>();
builder.Services.AddTransient<PitStopMappingViewModel>();
builder.Services.AddTransient<SopEvaluationViewModel>();
builder.Services.AddTransient<SmartRefuelingImplementationViewModel>();
builder.Services.AddTransient<ScrumProjectViewModel>();
builder.Services.AddTransient<ScrumProductBacklogViewModel>();
builder.Services.AddTransient<WhatsAppScrumTaskService>();
builder.Services.AddSingleton<ScrumProductBacklogImageStorage>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();
var pbiImageStorage = app.Services.GetRequiredService<ScrumProductBacklogImageStorage>();
Directory.CreateDirectory(pbiImageStorage.RootPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(pbiImageStorage.RootPath),
    RequestPath = "/uploads/pbi"
});
app.UseStaticFiles();

app.MapStaticAssets();
app.MapGet("/api/whatsapp/ft-summary/{district}", async Task<IResult> (
    string district,
    int? days,
    HttpRequest request,
    IConfiguration configuration,
    PitStopMappingService mappingService,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    var configuredApiKey = configuration["WhatsAppBot:ApiKey"];
    if (string.IsNullOrWhiteSpace(configuredApiKey))
    {
        return Results.Problem(
            "WhatsApp bot API is not configured.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    var providedApiKey = request.Headers["X-Bot-Api-Key"].ToString();
    var expectedBytes = Encoding.UTF8.GetBytes(configuredApiKey);
    var providedBytes = Encoding.UTF8.GetBytes(providedApiKey);
    if (!CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
    {
        return Results.Unauthorized();
    }

    var normalizedDistrict = district.Trim().ToUpperInvariant();
    if (normalizedDistrict.Length is < 2 or > 10 ||
        normalizedDistrict.Any(character => !char.IsAsciiLetterOrDigit(character)))
    {
        return Results.BadRequest(new { error = "Kode district tidak valid." });
    }

    var selectedDays = days ?? 7;
    if (selectedDays is < 1 or > 90)
    {
        return Results.BadRequest(new { error = "Periode harus antara 1 dan 90 hari." });
    }

    try
    {
        var summary = await mappingService.GetFuelTruckDistrictSummaryAsync(
            normalizedDistrict, selectedDays, cancellationToken);
        return Results.Ok(summary);
    }
    catch (SqlException exception)
    {
        logger.LogError(exception, "Failed to get Fuel Truck summary for district {District}.", normalizedDistrict);
        return Results.Problem(
            "Data Fuel Truck gagal dimuat.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (InvalidOperationException exception)
    {
        logger.LogError(exception, "The RfidDb connection string is unavailable.");
        return Results.Problem(
            "Koneksi RfidDb belum dikonfigurasi.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
})
    .AllowAnonymous();
app.MapPost("/api/whatsapp/smare/my-tasks", async Task<IResult> (
    WhatsAppScrumTaskRequest input,
    HttpRequest request,
    IConfiguration configuration,
    WhatsAppScrumTaskService taskService,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    var configuredApiKey = configuration["WhatsAppBot:ApiKey"];
    if (string.IsNullOrWhiteSpace(configuredApiKey))
    {
        return Results.Problem(
            "WhatsApp bot API is not configured.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    var providedApiKey = request.Headers["X-Bot-Api-Key"].ToString();
    var expectedBytes = Encoding.UTF8.GetBytes(configuredApiKey);
    var providedBytes = Encoding.UTF8.GetBytes(providedApiKey);
    if (!CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
    {
        return Results.Unauthorized();
    }

    if (!MasterPicContact.TryNormalizePhoneNumber(input.PhoneNumber, out var phoneNumber) ||
        !MasterPicContact.TryNormalizeWhatsAppId(input.WhatsAppId, out var whatsAppId))
    {
        return Results.BadRequest(new { error = "Nomor telepon atau WhatsApp ID tidak valid." });
    }

    if (phoneNumber is null && whatsAppId is null)
    {
        return Results.BadRequest(new { error = "Nomor telepon atau WhatsApp ID wajib dikirim." });
    }

    try
    {
        var result = await taskService.GetMyTasksAsync(phoneNumber, whatsAppId, cancellationToken);
        if (result.IdentityAmbiguous)
        {
            return Results.Conflict(new
            {
                error = "Identitas WhatsApp cocok dengan lebih dari satu PIC. Periksa Master PIC."
            });
        }

        if (!result.PicFound)
        {
            return Results.NotFound(new
            {
                error = "Nomor WhatsApp belum terdaftar pada Master PIC."
            });
        }

        return Results.Ok(result);
    }
    catch (SqlException exception)
    {
        logger.LogError(exception, "Failed to get Smart Refueling Scrum tasks for a WhatsApp PIC.");
        return Results.Problem(
            "Data Scrum Smart Refueling gagal dimuat.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
})
    .AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
