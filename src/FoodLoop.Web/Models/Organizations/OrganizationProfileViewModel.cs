using System;
using System.ComponentModel.DataAnnotations;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Web.Models.Organizations;

public class OrganizationProfileViewModel
{
    public Guid Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Address { get; set; } = string.Empty;

    public string LicenseNumber { get; set; } = string.Empty;

    public OrganizationType Type { get; set; }

    public OrganizationStatus Status { get; set; }

    public bool IsReadOnly { get; set; }

    public byte[] RowVersion { get; set; } = [];
}