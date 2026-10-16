namespace FitnessCenter.Api.Booking;

public sealed class BookingService
{
    public const string DemoSlotId = "morning-yoga";

    private readonly object _gate = new();
    private readonly Dictionary<string, TimeSlot> _slots;
    private readonly List<ClientBooking> _bookings = [];

    public BookingService()
    {
        _slots = new Dictionary<string, TimeSlot>(StringComparer.Ordinal)
        {
            [DemoSlotId] = new TimeSlot(DemoSlotId, 2),
        };
    }

    public TimeSlot? FindSlot(string slotId)
    {
        lock (_gate)
        {
            return _slots.TryGetValue(slotId, out var slot) ? slot : null;
        }
    }

    public BookingDecision Book(string slotId, string clientId)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(slotId) || !_slots.TryGetValue(slotId, out var slot))
            {
                return new BookingDecision(false, BookingError.UnknownSlot, null, null);
            }

            var decision = BookingPolicy.TryBook(slot, clientId, _bookings);
            if (!decision.Accepted || decision.Created is null || decision.Slot is null)
            {
                return decision;
            }

            _slots[slotId] = decision.Slot;
            _bookings.Add(decision.Created);
            return decision;
        }
    }
}
