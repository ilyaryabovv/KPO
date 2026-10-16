using FitnessCenter.Api.Booking;

namespace FitnessCenter.Api.Tests;

public class BookingPolicyTests
{
    private static readonly TimeSlot Slot = new("morning-yoga", 2);

    [Fact]
    public void Books_client_and_decrements_free_seats()
    {
        var decision = BookingPolicy.TryBook(Slot, "client-1", []);

        Assert.True(decision.Accepted);
        Assert.Equal(BookingError.None, decision.Error);
        Assert.NotNull(decision.Slot);
        Assert.Equal(1, decision.Slot.FreeSeats);
        Assert.Equal(new ClientBooking("morning-yoga", "client-1"), decision.Created);
    }

    [Fact]
    public void Rejects_empty_client()
    {
        var decision = BookingPolicy.TryBook(Slot, "  ", []);

        Assert.False(decision.Accepted);
        Assert.Equal(BookingError.EmptyClient, decision.Error);
        Assert.Equal(2, decision.Slot!.FreeSeats);
        Assert.Null(decision.Created);
    }

    [Fact]
    public void Rejects_duplicate_booking()
    {
        var existing = new[] { new ClientBooking(Slot.Id, "client-1") };

        var decision = BookingPolicy.TryBook(Slot, "client-1", existing);

        Assert.False(decision.Accepted);
        Assert.Equal(BookingError.Duplicate, decision.Error);
        Assert.Equal(2, decision.Slot!.FreeSeats);
    }

    [Fact]
    public void Rejects_booking_when_no_seats_remain()
    {
        var full = Slot with { FreeSeats = 0 };

        var decision = BookingPolicy.TryBook(full, "client-1", []);

        Assert.False(decision.Accepted);
        Assert.Equal(BookingError.NoFreeSeats, decision.Error);
        Assert.Null(decision.Created);
    }
}
