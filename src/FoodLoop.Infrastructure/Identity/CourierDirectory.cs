using FoodLoop.Application.Courier;
using FoodLoop.Application.Identity;
using Microsoft.AspNetCore.Identity;
namespace FoodLoop.Infrastructure.Identity;
public sealed class CourierDirectory(UserManager<ApplicationUser> users) : ICourierDirectory
{
    public async Task<IReadOnlyList<CourierOption>> ListAsync() => (await users.GetUsersInRoleAsync(AppRoles.Courier)).Select(x => new CourierOption(x.Id, x.UserName ?? x.Id.ToString())).ToList();
    public async Task<bool> IsCourierAsync(Guid id) => await users.FindByIdAsync(id.ToString()) is { } user && await users.IsInRoleAsync(user, AppRoles.Courier);
}
