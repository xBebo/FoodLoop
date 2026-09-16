using System.ComponentModel.DataAnnotations;
using FoodLoop.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FoodLoop.Web.Models.Donations;

public sealed class CreateDonationViewModel
{
    [Required]
    [StringLength(200)]
    [Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Food category")]
    public Guid FoodCategoryId { get; set; }

    [Range(typeof(decimal), "0.001", "999999999.999")]
    public decimal Quantity { get; set; } = 1;

    [Required]
    public QuantityUnit Unit { get; set; } = QuantityUnit.Meals;

    [Required]
    [Display(Name = "Prepared at")]
    public DateTimeOffset PreparedAt { get; set; } = DateTimeOffset.Now;

    [Required]
    [Display(Name = "Expires at")]
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.Now.AddHours(2);

    [StringLength(1000)]
    [Display(Name = "Storage instructions")]
    public string StorageInstructions { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    [Display(Name = "Pickup address")]
    public string PickupAddress { get; set; } = string.Empty;

    public IReadOnlyList<SelectListItem> Categories { get; set; } = [];
}
