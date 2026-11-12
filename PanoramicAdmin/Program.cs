using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.FileProviders;
using MudBlazor.Services;
using PanoramicAdmin.Components;
using PanoramicAdmin.Services;
using PanoramicAdmin.ViewModels;

var builder = WebApplication.CreateBuilder(args);

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
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
