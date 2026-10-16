namespace FitnessCenter.Api.Booking;

public sealed record TimeSlot(string Id, int FreeSeats);

public sealed record ClientBooking(string SlotId, string ClientId);

public sealed record BookingRequest(string SlotId, string ClientId);

public enum BookingError
{
    None = 0,
    EmptyClient = 1,
    UnknownSlot = 2,
    NoFreeSeats = 3,
    Duplicate = 4,
}

public sealed record BookingDecision(
    bool Accepted,
    BookingError Error,
    TimeSlot? Slot,
    ClientBooking? Created);
