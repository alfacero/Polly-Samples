using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddRateLimiter(limiterOptions =>
{
    limiterOptions.AddFixedWindowLimiter("ThreeRequestsPerFiveSeconds", windowOptions =>
    {
        windowOptions.PermitLimit = 3;
        windowOptions.Window = TimeSpan.FromSeconds(5);
    });
    limiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    limiterOptions.OnRejected = async (context, _) =>
    {
        await context.HttpContext.Response.WriteAsync("Too many requests have received. Request refused.", CancellationToken.None);
    };
});

var app = builder.Build();

// Register the ValuesController that is rate limited.
app.MapControllers();
app.UseRateLimiter();

// Register two endpoints that are not rate limited.
// They are used by demos 10 and 11 (concurrency limiter)
app.MapGet("/api/NonThrottledGood/{id}", ([FromRoute] int id) =>
{
    return $"Fast response from server to request #{id}";
});
app.MapGet("/api/NonThrottledFaulting/{id}", async ([FromRoute] int id) =>
{
    await Task.Delay(TimeSpan.FromSeconds(5));
    return $"Slow response from server to request #{id}";
});

// Register a cancellable endpoint that is not rate limited.
// It is used by the demo 12 (hedging)
app.MapGet("/api/VaryingResponseTime/{id}", async (CancellationToken token, [FromRoute] string id) =>
{
    var jitter = Random.Shared.Next(-800, 800);
    var delay = TimeSpan.FromSeconds(1) + TimeSpan.FromMilliseconds(jitter);
    await Task.Delay(delay, token);
    return $"Deferred response with ~{delay.TotalMilliseconds}ms from server to request #{id}";
});

// Register a cancellable endpoint that is not rate limited.
// It is used by demos 13, 14 and 15 (hedging)
app.MapGet("/api/VaryingResponseStatus/{id}", async (CancellationToken token, [FromRoute] string id, [FromQuery] bool? useJitter) =>
{
    var jitter = Random.Shared.Next(-200, 200);
    var delay = TimeSpan.FromSeconds(0.5);
    if (useJitter == true)
    {
        delay += TimeSpan.FromMilliseconds(jitter);
    }
    await Task.Delay(delay);

    var isSuccess = Random.Shared.NextDouble() > 0.5d;
    return isSuccess
        ? Results.Ok($"Success response with from server to request #{id}")
        : Results.Problem($"Failed response with from server to request #{id}", statusCode: StatusCodes.Status418ImATeapot);
});

app.Run("http://localhost:45179");
