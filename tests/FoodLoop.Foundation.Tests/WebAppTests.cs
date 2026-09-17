using System.Text;
using System.Text.RegularExpressions;
using FoodLoop.Application;
using FoodLoop.Application.Claims;
using FoodLoop.Domain.Enums;
using FoodLoop.Web.Controllers;
using FoodLoop.Web.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
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
        Assert.Contains("/Auth/Login?ReturnUrl=", response.Headers.Location!.OriginalString);
    }
    [Fact]
    public async Task Cancel_claim_post_without_antiforgery_token_is_rejected()
    {
        var response = await Client().PostAsync("/Claims/Cancel", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["claimId"] = Guid.NewGuid().ToString() }));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Fact]
    public async Task Cancel_claim_post_with_token_from_the_rendered_form_reaches_the_action()
    {
        // The token comes from the real My Claims form, proving the form tag helper emits a token the global filter accepts.
        using var scope = factory.Services.CreateScope();
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var html = await RenderMineAsync(factory, new MyClaimsViewModel([Summary(ClaimStatus.Booked, canCancel: true)], 1, 20), httpContext: httpContext);
        var formToken = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(formToken);
        var cookie = httpContext.Response.Headers.SetCookie.ToString().Split(';')[0];

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Claims/Cancel")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            { ["claimId"] = Guid.NewGuid().ToString(), ["__RequestVerificationToken"] = formToken })
        };
        request.Headers.Add("Cookie", cookie);
        var response = await Client().SendAsync(request);
        // Anonymous caller: antiforgery passed, the action ran and ClaimService's Unauthenticated outcome became a login challenge.
        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Auth/Login?ReturnUrl=", response.Headers.Location!.OriginalString);
    }

    // ---- My Claims Razor view, rendered by the real view engine without auth or database (authorization is covered by the controller tests).
    private Task<string> RenderMineAsync(MyClaimsViewModel model, string? success = null, string? error = null) => RenderMineAsync(factory, model, success, error);
    internal static async Task<string> RenderMineAsync(WebApplicationFactory<Program> factory, MyClaimsViewModel model,
        string? success = null, string? error = null, DefaultHttpContext? httpContext = null)
    {
        using var scope = factory.Services.CreateScope();
        var routeValues = new RouteValueDictionary { ["controller"] = "Claims", ["action"] = "Mine" };
        httpContext ??= new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        httpContext.Request.RouteValues = routeValues;
        httpContext.SetEndpoint(new Endpoint(null, null, "Claims/Mine")); // Makes tag helpers use endpoint-routing link generation, as in the real app.
        using var body = new MemoryStream();
        httpContext.Response.Body = body;
        var tempData = new TempDataDictionary(httpContext, httpContext.RequestServices.GetRequiredService<ITempDataProvider>());
        if (success is not null) tempData["Success"] = success;
        if (error is not null) tempData["Error"] = error;
        var view = new ViewResult
        {
            ViewName = nameof(ClaimsController.Mine),
            ViewData = new ViewDataDictionary<MyClaimsViewModel>(httpContext.RequestServices.GetRequiredService<IModelMetadataProvider>(), new ModelStateDictionary()) { Model = model },
            TempData = tempData
        };
        await view.ExecuteResultAsync(new ActionContext(httpContext, new RouteData(routeValues), new ControllerActionDescriptor { RouteValues = { ["controller"] = "Claims", ["action"] = "Mine" } }));
        return Encoding.UTF8.GetString(body.ToArray());
    }
    private static ClaimSummary Summary(ClaimStatus status, string title = "Bread", bool canCancel = false) => new(Guid.NewGuid(), Guid.NewGuid(), title, 12.5m, QuantityUnit.Kilograms,
        "1 Main Street", new DateTimeOffset(2026, 9, 20, 18, 0, 0, TimeSpan.Zero), status, new DateTimeOffset(2026, 9, 14, 9, 5, 0, TimeSpan.Zero), canCancel);

    [Fact]
    public async Task Mine_view_renders_every_status_with_readable_label_and_honest_progress()
    {
        var html = await RenderMineAsync(new MyClaimsViewModel([.. Enum.GetValues<ClaimStatus>().Select(s => Summary(s))], 1, 20));
        foreach (var label in new[] { "Booked", "Pickup Pending", "Picked Up", "In Transit", "Delivered", "Closed", "Cancelled", "Failed" })
            Assert.Contains($"</span>{label}", html);
        for (var step = 1; step <= 6; step++) Assert.Contains($"Step {step} of 6", html);
        Assert.Equal(2, Regex.Matches(html, "No further steps").Count);
        Assert.Contains("12.5 Kilograms", html);
        Assert.Matches("<time [^>]*datetime=\"2026-09-14T09:05:00Z\">14 Sep 2026, 09:05 UTC</time>", html);
        Assert.Contains("On this page", html);
        Assert.DoesNotContain("Total", html);
        Assert.DoesNotContain("No claims yet", html);
    }
    [Fact]
    public async Task Mine_view_html_encodes_claim_data_and_feedback_messages()
    {
        var html = await RenderMineAsync(new MyClaimsViewModel([Summary(ClaimStatus.Booked, "<script>alert(1)</script>")], 1, 20),
            success: "<b>Donation claimed.</b>", error: "<img src=x>");
        Assert.DoesNotContain("<script>alert(1)", html);
        Assert.DoesNotContain("<b>Donation", html);
        Assert.DoesNotContain("<img src=x>", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.Contains("role=\"status\"", html);
        Assert.Contains("role=\"alert\"", html);
    }
    [Fact]
    public async Task Mine_view_empty_first_page_shows_empty_state_without_pagination()
    {
        var html = await RenderMineAsync(new MyClaimsViewModel([], 1, 20));
        Assert.Contains("No claims yet", html);
        Assert.DoesNotContain("<table", html);
        Assert.DoesNotContain("aria-label=\"My claims pages\"", html);
        Assert.DoesNotContain("role=\"alert\"", html);
    }
    [Fact]
    public async Task Mine_view_pagination_links_preserve_page_size_and_never_claim_a_total()
    {
        var full = await RenderMineAsync(new MyClaimsViewModel([Summary(ClaimStatus.Booked), Summary(ClaimStatus.Delivered)], 2, 2));
        Assert.Contains("href=\"/Claims/Mine?page=1&amp;pageSize=2\"", full);
        Assert.Contains("href=\"/Claims/Mine?page=3&amp;pageSize=2\"", full);
        Assert.Contains("Page 2", full);
        Assert.DoesNotMatch("Page 2 of|organization[Ii]d", full);

        var partial = await RenderMineAsync(new MyClaimsViewModel([Summary(ClaimStatus.Booked)], 1, 2));
        Assert.DoesNotContain("page=2", partial);
        Assert.DoesNotContain("aria-label=\"My claims pages\"", partial);

        var beyondEnd = await RenderMineAsync(new MyClaimsViewModel([], 3, 2));
        Assert.Contains("No claims on this page", beyondEnd);
        Assert.Contains("href=\"/Claims/Mine?page=2&amp;pageSize=2\"", beyondEnd);
        Assert.DoesNotContain("page=4", beyondEnd);
    }
    [Fact]
    public async Task Mine_view_renders_post_cancel_form_with_confirmation_only_for_cancellable_claims()
    {
        var cancellable = Summary(ClaimStatus.Booked, "Soup", canCancel: true);
        var html = await RenderMineAsync(new MyClaimsViewModel([cancellable, Summary(ClaimStatus.Booked), Summary(ClaimStatus.PickupPending)], 1, 20));
        var form = Assert.Single(Regex.Matches(html, "<form[^>]*>.*?</form>", RegexOptions.Singleline)).Value;
        Assert.Contains("method=\"post\"", form);
        Assert.Contains("action=\"/Claims/Cancel\"", form);
        Assert.Contains("onsubmit=\"return confirm('Cancel this claim? This action cannot be undone.');\"", form);
        Assert.Matches($"<input [^>]*type=\"hidden\" name=\"claimId\" value=\"{cancellable.ClaimId}\"", form);
        Assert.Matches("<input name=\"__RequestVerificationToken\" type=\"hidden\" value=\"[^\"]+\"", form);
        Assert.Matches("<button [^>]*type=\"submit\"[^>]*>Cancel claim<span [^>]*class=\"visually-hidden\">: Soup</span></button>", form);
        // Only claimId and the antiforgery token are posted: no ownership, status or organization input.
        Assert.Equal(["claimId", "__RequestVerificationToken"], Regex.Matches(form, "name=\"([^\"]+)\"").Select(m => m.Groups[1].Value));
        Assert.DoesNotMatch("Marketplace|available again", form);
        Assert.Matches("<th [^>]*scope=\"col\">Actions</th>", html);
        // Non-cancellable rows get a truly empty cell (hidden on mobile by :empty), never a disabled button.
        Assert.Equal(3, Regex.Matches(html, "<td [^>]*class=\"claims-actions\"").Count);
        Assert.Equal(2, Regex.Matches(html, "<td [^>]*class=\"claims-actions\"></td>").Count);
        Assert.DoesNotContain("disabled", html);
    }
    [Fact]
    public async Task Mine_view_without_cancellable_claims_is_read_only()
    {
        var html = await RenderMineAsync(new MyClaimsViewModel([.. Enum.GetValues<ClaimStatus>().Select(s => Summary(s))], 1, 20));
        Assert.DoesNotContain("<form", html);
        Assert.DoesNotContain("Cancel claim", html);
        Assert.DoesNotContain("Actions", html);
        Assert.DoesNotContain("claims-actions", html);
    }
}
