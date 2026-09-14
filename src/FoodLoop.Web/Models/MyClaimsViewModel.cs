using FoodLoop.Application.Claims;
namespace FoodLoop.Web.Models;
public sealed record MyClaimsViewModel(IReadOnlyList<ClaimSummary> Claims, int Page, int PageSize);
