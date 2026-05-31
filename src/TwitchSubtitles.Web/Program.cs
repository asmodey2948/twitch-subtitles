using System.Reflection;
using Microsoft.AspNetCore.Http;
using TwitchSubtitles.Web.Handlers;
using TwitchSubtitles.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient<IMlServiceClient, MlServiceClient>();
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<WebSocketBroadcaster>();
builder.Services.AddTransient<SubtitlesWebSocketHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});
app.MapControllers();

// Version endpoint
app.MapGet("/api/version", async (HttpContext context, IMlServiceClient mlClient) =>
{
    var assembly = Assembly.GetExecutingAssembly();
    var assemblyName = assembly.GetName();
    var backendVersion = assemblyName.Version?.ToString(3) ?? "unknown";

    string mlVersion = "unknown";
    try
    {
        var mlResponse = await mlClient.GetVersionAsync();
        mlVersion = mlResponse.Version ?? "unknown";
    }
    catch
    {
        mlVersion = "unavailable";
    }

    return Results.Ok(new { backend = backendVersion, ml = mlVersion });
});

// WebSocket endpoint для streaming субтитров
app.Map("/ws/subtitles", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var handler = app.Services.GetRequiredService<SubtitlesWebSocketHandler>();
        await handler.HandleAsync(webSocket);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
    }
});

app.Run();

/// <summary>
/// Точка входа — необходима для доступа к Program в тестах (WebApplicationFactory).
/// </summary>
public partial class Program;
