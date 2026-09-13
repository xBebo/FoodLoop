using FoodLoop.Domain.Common;
using FoodLoop.Domain.Enums;
namespace FoodLoop.Domain.Entities;
public sealed class Organization : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;
    public OrganizationType Type { get; set; }
    public OrganizationStatus Status { get; set; } = OrganizationStatus.Pending;
    public string Address { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];
}
