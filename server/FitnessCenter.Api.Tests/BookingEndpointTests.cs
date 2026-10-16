using System.Net;
using System.Net.Http.Json;
using FitnessCenter.Api.Booking;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FitnessCenter.Api.Tests;

public sealed class BookingEndpointTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory = new();

    [Fact]
    public async Task Health_returns_ok()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Booking_decrements_free_seats()
    {
        var client = _factory.CreateClient();

        var response = await PostBooking(client, BookingService.DemoSlotId, "client-1");
        response.EnsureSuccessStatusCode();

        var slot = await client.GetFromJsonAsync<TimeSlot>("/api/slots/" + BookingService.DemoSlotId);

        Assert.NotNull(slot);
        Assert.Equal(1, slot.FreeSeats);
    }

    [Fact]
    public async Task Second_booking_of_the_same_client_is_rejected()
    {
        var client = _factory.CreateClient();

        var first = await PostBooking(client, BookingService.DemoSlotId, "client-1");
        var second = await PostBooking(client, BookingService.DemoSlotId, "client-1");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var slot = await client.GetFromJsonAsync<TimeSlot>("/api/slots/" + BookingService.DemoSlotId);
        Assert.NotNull(slot);
        Assert.Equal(1, slot.FreeSeats);
    }

    [Fact]
    public async Task Third_client_is_rejected_when_the_slot_is_full()
    {
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await PostBooking(client, BookingService.DemoSlotId, "client-1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostBooking(client, BookingService.DemoSlotId, "client-2")).StatusCode);

        var third = await PostBooking(client, BookingService.DemoSlotId, "client-3");

        Assert.Equal(HttpStatusCode.BadRequest, third.StatusCode);
    }

    [Fact]
    public async Task Unknown_slot_is_not_found()
    {
        var client = _factory.CreateClient();

        var response = await PostBooking(client, "missing-slot", "client-1");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    private static Task<HttpResponseMessage> PostBooking(HttpClient client, string slotId, string clientId)
    {
        return client.PostAsJsonAsync("/api/bookings", new BookingRequest(slotId, clientId));
    }
}
