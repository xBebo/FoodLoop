using FoodLoop.Domain.Enums;

namespace FoodLoop.Web.Models.Courier;

public sealed record HandoverCodeViewModel(
    string RawToken,
    HandoverType HandoverType,
    DateTimeOffset ExpiresAtUtc,
    string QrSvg);
