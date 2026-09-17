using FoodLoop.Application.Donations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FoodLoop.Web.Models.Donations;

public sealed record AvailableDonationsViewModel(
    IReadOnlyList<DonationListItem> Items,
    IReadOnlyList<SelectListItem> Categories,
    string Search,
    Guid? CategoryId,
    int Page,
    bool HasPrevious,
    bool HasNext);
