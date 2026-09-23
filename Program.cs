using FireOps.Components;
using FireOps.Data;
using FireOps.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "data");
Directory.CreateDirectory(dataDirectory);

var configuredConnectionString = builder.Configuration.GetConnectionString("FireOps");
var connectionString = string.IsNullOrWhiteSpace(configuredConnectionString)
    ? $"Data Source={Path.Combine(dataDirectory, "fireops.db")}" 
    : configuredConnectionString.Replace("{DataDirectory}", dataDirectory, StringComparison.OrdinalIgnoreCase);

builder.Services.AddDbContextFactory<FireOpsDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddSingleton<ChangeNotifier>();
builder.Services.AddScoped<FireOpsService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await DbInitializer.InitializeAsync(app.Services);

app.Run();
