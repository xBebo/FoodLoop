using FoodLoop.Application;
using FoodLoop.Application.Claims;
using FoodLoop.Web.Controllers;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FoodLoop.Foundation.Tests;
// Hosts the real Program pipeline. None of these requests reach the database: antiforgery and the unauthenticated check run first.
public sealed class WebAppTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions
    { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    [Fact]
    public void AddApplication_registers_ClaimService_once_as_scoped()
    {
        var descriptor = Assert.Single(new ServiceCollection().AddApplication(), x => x.ServiceType == typeof(ClaimService));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }
    [Fact]
    public void Web_app_startup_resolves_ClaimService_and_ClaimsController()
    {
        using var scope = factory.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ClaimService>());
        Assert.NotNull(ActivatorUtilities.CreateInstance<ClaimsController>(scope.ServiceProvider));
    }
    [Fact]
    public async Task Create_claim_post_without_antiforgery_token_is_rejected()
    {
        var response = await Client().PostAsync("/Claims/Create", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["donationId"] = Guid.NewGuid().ToString() }));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Fact]
    public async Task Create_claim_post_with_valid_antiforgery_token_reaches_the_action()
    {
        using var scope = factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<IAntiforgery>().GetAndStoreTokens(new DefaultHttpContext { RequestServices = scope.ServiceProvider });
        var cookieName = scope.ServiceProvider.GetRequiredService<IOptions<AntiforgeryOptions>>().Value.Cookie.Name;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Claims/Create")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            { ["donationId"] = Guid.NewGuid().ToString(), [tokens.FormFieldName] = tokens.RequestToken! })
        };
        request.Headers.Add("Cookie", $"{cookieName}={tokens.CookieToken}");
        var response = await Client().SendAsync(request);
        // Anonymous caller: antiforgery passed, the action ran and ClaimService's Unauthenticated outcome became a login challenge.
        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.OriginalString);
    }
}
