using FoodLoop.Domain.Common;
using FoodLoop.Domain.Enums;
namespace FoodLoop.Domain.Entities;
public sealed class FoodCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
}
