using BluffGame.Server.AI;
using BluffGame.Server.Game;
using BluffGame.Server.Hubs;
using BluffGame.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Services ---
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
});

builder.Services.AddSingleton<GameEngine>();
builder.Services.AddSingleton<BotEngine>();
builder.Services.AddSingleton<IRoomManager, RoomManager>();
builder.Services.AddSingleton<IGameCoordinator, GameCoordinator>();
builder.Services.AddHostedService<RoomCleanupService>();

// CORS for Angular dev server
builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// --- Pipeline ---
if (app.Environment.IsDevelopment())
{
    app.UseCors("Development");
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHub<GameHub>("/gamehub");

// Health check for Render keep-alive
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// SPA fallback — serve index.html for Angular routes
app.MapFallbackToFile("index.html");

app.Run();
