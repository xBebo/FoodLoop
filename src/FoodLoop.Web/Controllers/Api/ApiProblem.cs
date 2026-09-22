using Microsoft.AspNetCore.Mvc;

namespace FoodLoop.Web.Controllers.Api;

public static class ApiProblem
{
    // Sanitized ProblemDetails with a stable machine-readable code; the SPA branches on the code, never on text.
    public static ObjectResult Fail(this ControllerBase controller, int status, string code)
    {
        var result = (ObjectResult)controller.Problem(statusCode: status);
        ((ProblemDetails)result.Value!).Extensions["code"] = code;
        return result;
    }
}
