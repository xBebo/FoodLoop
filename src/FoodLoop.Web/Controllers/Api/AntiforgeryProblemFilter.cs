using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Core.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace FoodLoop.Web.Controllers.Api;

// Tags an /api antiforgery rejection with its own code, so the SPA can tell a stale token from a form error.
// Runs before the ApiController client-error filter would turn it into a generic 400.
public sealed class AntiforgeryProblemFilter : IAlwaysRunResultFilter, IOrderedFilter
{
    public int Order => -3000;

    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not IAntiforgeryValidationFailedResult || !context.HttpContext.Request.Path.StartsWithSegments("/api")) return;
        var problem = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>()
            .CreateProblemDetails(context.HttpContext, StatusCodes.Status400BadRequest);
        problem.Extensions["code"] = "antiforgery.invalid";
        context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status400BadRequest, ContentTypes = { "application/problem+json" } };
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}
