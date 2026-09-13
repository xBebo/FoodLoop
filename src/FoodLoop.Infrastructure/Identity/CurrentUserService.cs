using System.Security.Claims;
using FoodLoop.Application.Interfaces.Identity;
using FoodLoop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
namespace FoodLoop.Infrastructure.Identity;
public sealed class CurrentUserService(IHttpContextAccessor accessor, ApplicationDbContext db) : ICurrentUserService
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public Guid? UserId => IsAuthenticated && Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public bool IsInRole(string role) => IsAuthenticated && Principal?.IsInRole(role) == true;
    public Task<Guid?> GetOrganizationIdAsync(CancellationToken cancellationToken = default)
        => UserId is Guid id
            ? db.Users.Where(x => x.Id == id).Select(x => x.OrganizationId).SingleOrDefaultAsync(cancellationToken)
            : Task.FromResult<Guid?>(null);
}
