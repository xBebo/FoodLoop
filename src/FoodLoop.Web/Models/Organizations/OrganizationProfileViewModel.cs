using System.ComponentModel.DataAnnotations;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Web.Models.Organizations;

public sealed class OrganizationProfileViewModel
{
    public Guid Id { get; init; }

    [Required(ErrorMessage = "Name is required.")]
    [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = string.Empty;

    public string LicenseNumber { get; init; } = string.Empty;
    public OrganizationType Type { get; init; }
    public OrganizationStatus Status { get; init; }

    [Required(ErrorMessage = "Address is required.")]
    [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters.")]
    public string Address { get; set; } = string.Empty;

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public bool IsReadOnly => Status != OrganizationStatus.Active;
}