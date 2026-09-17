using System.ComponentModel.DataAnnotations;
using FoodLoop.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FoodLoop.Web.Models.Donations;

public sealed class EditDonationViewModel
{
    public Guid Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Food category")]
    public Guid FoodCategoryId { get; set; }

    [Range(typeof(decimal), "0.001", "999999999.999")]
    public decimal Quantity { get; set; }

    [Required]
    public QuantityUnit Unit { get; set; }

    [Required]
    [Display(Name = "Prepared at")]
    public DateTimeOffset PreparedAt { get; set; }

    [Required]
    [Display(Name = "Expires at")]
    public DateTimeOffset ExpiresAt { get; set; }

    [StringLength(1000)]
    [Display(Name = "Storage instructions")]
    public string StorageInstructions { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    [Display(Name = "Pickup address")]
    public string PickupAddress { get; set; } = string.Empty;

    [Required]
    public string RowVersion { get; set; } = string.Empty;

    public IReadOnlyList<SelectListItem> Categories { get; set; } = [];
}
