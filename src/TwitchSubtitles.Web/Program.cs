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
