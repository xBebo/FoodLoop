using FoodLoop.Infrastructure;
using FoodLoop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection before running outside Development.");
    connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=FoodLoop_Development;Trusted_Connection=True;TrustServerCertificate=True";
}
builder.Services.AddInfrastructure(connectionString);
var app = builder.Build();

// Explicit development command. Normal startup never creates or migrates a database.
if (args.Contains("--seed", StringComparer.OrdinalIgnoreCase))
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Development seeding is disabled outside Development.");
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if ((await db.Database.GetPendingMigrationsAsync()).Any())
        throw new InvalidOperationException("Apply migrations first; see README.md.");
    await scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>()
        .SeedAsync(app.Configuration["Seed:DemoPassword"]);
    app.Logger.LogInformation("Development seed completed. Accounts are created only when Seed:DemoPassword is configured.");
    return;
}
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}").WithStaticAssets();
app.Run();
