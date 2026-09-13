namespace FoodLoop.Application.Identity;
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Donor = "Donor";
    public const string Beneficiary = "Beneficiary";
    public const string Courier = "Courier";
    public static IReadOnlyList<string> All { get; } = [Admin, Donor, Beneficiary, Courier];
}
