using FoodLoop.Application;
using FoodLoop.Infrastructure;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Web.Controllers.Api;
using FoodLoop.Web.Services;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        options.Filters.Add(new AntiforgeryProblemFilter());
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        // A malformed body gets a generic 400 message, never serializer text naming internal types.
        options.AllowInputFormatterExceptionMessages = false;
    });
// The SPA sends the request token in this header; the global antiforgery filter above covers /api too.
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    if (!builder.Environment.IsDevelopment())
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});
// Every /api error carries a stable machine-readable code; endpoints set a more specific one where it matters.
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions.TryAdd("code", context.ProblemDetails.Status switch
    {
        400 => "validation",
        401 => "auth.unauthenticated",
        403 => "auth.forbidden",
        404 => "not_found",
        409 => "conflict",
        >= 500 => "server.error",
        _ => "error"
    }));
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection before running outside Development.");
    connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=FoodLoop_Development;Trusted_Connection=True;TrustServerCertificate=True";
}
builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString, persistDataProtectionKeys: !builder.Environment.IsDevelopment());

var reverseProxyEnabled = builder.Configuration.GetValue<bool>("ReverseProxy:Enabled");
if (reverseProxyEnabled)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // Only enable this setting behind a trusted hosting proxy. Vercel is the intended production host.
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}
// MVC keeps its login / access-denied redirects; /api callers get a bare 401/403 that the API status pages turn into ProblemDetails.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = ApiStatusOr(StatusCodes.Status401Unauthorized, options.Events.OnRedirectToLogin);
    options.Events.OnRedirectToAccessDenied = ApiStatusOr(StatusCodes.Status403Forbidden, options.Events.OnRedirectToAccessDenied);
    if (!builder.Environment.IsDevelopment())
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});
builder.Services.Configure<DonationExpirySchedulerOptions>(
    builder.Configuration.GetSection(DonationExpirySchedulerOptions.SectionName));
builder.Services.AddHostedService<DonationExpiryBackgroundService>();
var app = builder.Build();

// Explicit production bootstrap. Normal startup never creates or migrates a database.
if (args.Contains("--initialize-production", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if ((await db.Database.GetPendingMigrationsAsync()).Any())
        throw new InvalidOperationException("Apply migrations before production initialization.");

    await scope.ServiceProvider.GetRequiredService<ReferenceDataSeeder>().SeedAsync();
    await scope.ServiceProvider.GetRequiredService<ProductionBootstrapSeeder>().SeedAsync(
        app.Configuration["Bootstrap:AdminEmail"],
        app.Configuration["Bootstrap:AdminPassword"],
        app.Configuration["Bootstrap:CourierEmail"],
        app.Configuration["Bootstrap:CourierPassword"]);

    app.Logger.LogInformation("Production initialization completed.");
    return;
}

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
if (reverseProxyEnabled)
    app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
// /api always answers errors with sanitized ProblemDetails JSON: never the developer page, MVC error HTML or an empty body.
app.UseWhen(context => IsApi(context.Request), api =>
{
    api.UseExceptionHandler();
    api.UseStatusCodePages();
});
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}").WithStaticAssets();
app.Run();

static bool IsApi(HttpRequest request) => request.Path.StartsWithSegments("/api");

static Func<RedirectContext<CookieAuthenticationOptions>, Task> ApiStatusOr(int status, Func<RedirectContext<CookieAuthenticationOptions>, Task> mvc) =>
    context =>
    {
        if (!IsApi(context.Request)) return mvc(context);
        context.Response.StatusCode = status;
        return Task.CompletedTask;
    };

// Exposes the entry point to HTTP integration tests.
public partial class Program { }
