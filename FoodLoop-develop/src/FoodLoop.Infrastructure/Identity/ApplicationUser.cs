using FoodLoop.Domain.Entities;
using Microsoft.AspNetCore.Identity;
namespace FoodLoop.Infrastructure.Identity;
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}
