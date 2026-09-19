using System.Globalization;
using System.Text.RegularExpressions;
using FoodLoop.Domain.Enums;
namespace FoodLoop.Web.Models;
// Display formatting shared by the claim views (My Claims and Claim Details).
public static partial class ClaimDisplay
{
    public static string Label(Enum value) => WordBoundary().Replace(value.ToString(), " ");
    public static string Tone(ClaimStatus status) => status switch
    {
        ClaimStatus.Booked or ClaimStatus.PickupPending => "pending",
        ClaimStatus.PickedUp or ClaimStatus.InTransit => "transit",
        ClaimStatus.Delivered or ClaimStatus.Closed => "done",
        ClaimStatus.Cancelled or ClaimStatus.Failed => "stopped",
        _ => "neutral"
    };
    // TODO: shown in UTC with an explicit label; switch to the viewer's timezone once a shared localization approach exists.
    public static string DisplayUtc(DateTimeOffset value) => value.ToUniversalTime().ToString("d MMM yyyy, HH:mm 'UTC'", CultureInfo.InvariantCulture);
    public static string IsoUtc(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    public static string Quantity(decimal quantity, QuantityUnit unit) => $"{quantity.ToString("0.##", CultureInfo.InvariantCulture)} {Label(unit)}";

    [GeneratedRegex("(?<=[a-z])(?=[A-Z])")]
    private static partial Regex WordBoundary();
}
