using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
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

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
