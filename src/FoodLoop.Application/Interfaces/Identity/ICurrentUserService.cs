namespace FoodLoop.Application.Interfaces.Identity;
public interface ICurrentUserService
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
    Task<Guid?> GetOrganizationIdAsync(CancellationToken cancellationToken = default);
}
