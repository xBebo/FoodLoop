using Microsoft.AspNetCore.Mvc;

namespace FoodLoop.Web.Controllers.Api;

public static class ApiProblem
{
    // Sanitized ProblemDetails with a stable machine-readable code; the SPA branches on the code, never on text.
    // detail is only ever a service's own user-facing message (e.g. a donation validation rule), never exception text.
    public static ObjectResult Fail(this ControllerBase controller, int status, string code, string? detail = null)
    {
        var result = (ObjectResult)controller.Problem(statusCode: status, detail: detail);
        ((ProblemDetails)result.Value!).Extensions["code"] = code;
        return result;
    }
}
