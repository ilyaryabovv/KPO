namespace FitnessCenter.Api.Booking;

public static class BookingPolicy
{
    public static BookingDecision TryBook(
        TimeSlot slot,
        string clientId,
        IReadOnlyCollection<ClientBooking> existing)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return Reject(BookingError.EmptyClient, slot);
        }

        var duplicate = existing.Any(booking =>
            booking.SlotId == slot.Id &&
            string.Equals(booking.ClientId, clientId, StringComparison.Ordinal));
        if (duplicate)
        {
            return Reject(BookingError.Duplicate, slot);
        }

        if (slot.FreeSeats <= 0)
        {
            return Reject(BookingError.NoFreeSeats, slot);
        }

        var updated = slot with { FreeSeats = slot.FreeSeats - 1 };
        return new BookingDecision(true, BookingError.None, updated, new ClientBooking(slot.Id, clientId));
    }

    private static BookingDecision Reject(BookingError error, TimeSlot slot)
    {
        return new BookingDecision(false, error, slot, null);
    }
}
