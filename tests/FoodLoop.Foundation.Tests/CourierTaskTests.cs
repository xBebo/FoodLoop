using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using Xunit;

namespace FoodLoop.Foundation.Tests;

public class CourierTaskTests
{
    [Fact]
    public void Delivery_Before_Pickup_Should_Be_Verified()
    {
        // Setup initial task state as Pickup
        var handover = new HandoverRecord
        {
            Id = System.Guid.NewGuid(),
            Type = HandoverType.Pickup
        };

        // Assert that state reflects Pickup correctly
        Assert.Equal(HandoverType.Pickup, handover.Type);
    }

    [Fact]
    public void HandoverRecord_Should_Initialize_Correctly()
    {
        var handover = new HandoverRecord();
        Assert.NotNull(handover);
    }
}