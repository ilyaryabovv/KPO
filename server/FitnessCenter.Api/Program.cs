using System.Text.Json.Serialization;
using FitnessCenter.Api.Booking;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSingleton<BookingService>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/slots/{id}", (string id, BookingService bookings) =>
{
    var slot = bookings.FindSlot(id);
    return slot is null ? Results.NotFound() : Results.Ok(slot);
});

app.MapPost("/api/bookings", (BookingRequest request, BookingService bookings) =>
{
    var decision = bookings.Book(request.SlotId, request.ClientId);
    if (decision.Accepted)
    {
        return Results.Ok(decision);
    }

    return decision.Error switch
    {
        BookingError.UnknownSlot => Results.NotFound(decision),
        BookingError.Duplicate => Results.Conflict(decision),
        _ => Results.BadRequest(decision),
    };
});

app.Run();

public partial class Program
{
}
