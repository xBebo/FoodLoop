using FoodLoop.Application.Donations;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Web.Models.Donations;

public sealed record MyDonationsViewModel(
    IReadOnlyList<DonationListItem> Items,
    DonationStatus? Status);
