using FireOps.Components;
using FireOps.Data;
using FireOps.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContextFactory<FireOpsDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("FireOps") ?? "Data Source=fireops.db"));

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

