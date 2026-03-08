using System.Security.Claims;
using BluffGame.Server.AI;
using BluffGame.Server.Auth;
using BluffGame.Server.Game;
using BluffGame.Server.Hubs;
using BluffGame.Server.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// --- Auth settings ---
var authSettings = new AuthSettings
{
    GoogleClientId = builder.Configuration["Auth:GoogleClientId"]
        ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID"),
    JwtSecret = builder.Configuration["Auth:JwtSecret"]
        ?? Environment.GetEnvironmentVariable("JWT_SECRET")
        ?? "BluffGame-Dev-Secret-Key-Change-In-Production-Min32Chars!",
    JwtIssuer = "BluffGame",
    JwtAudience = "BluffGame",
    JwtExpirationHours = 24
};

builder.Services.AddSingleton(authSettings);
builder.Services.AddSingleton<GoogleTokenValidator>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<UserMappingService>();

// --- JWT Authentication ---
var jwtService = new JwtTokenService(authSettings);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = jwtService.GetValidationParameters();

        // SignalR sends JWT via query string for WebSockets
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/gamehub"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// --- Services ---
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
}).AddHubOptions<GameHub>(options =>
{
    options.AddFilter(typeof(HubRateLimitFilter));
});

builder.Services.AddSingleton<HubRateLimitFilter>();
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

app.UseAuthentication();
app.UseAuthorization();

// --- Auth endpoints ---
app.MapPost("/api/auth/google", async (
    GoogleLoginRequest request,
    GoogleTokenValidator validator,
    JwtTokenService jwt,
    UserMappingService users,
    IRoomManager rooms) =>
{
    var googleUser = await validator.ValidateAsync(request.IdToken);
    if (googleUser is null)
        return Results.Unauthorized();

    var playerId = users.GetOrCreatePlayerId(googleUser);
    var token = jwt.GenerateToken(playerId, googleUser.GoogleId,
        googleUser.Name, googleUser.Email, googleUser.Picture);

    return Results.Ok(new AuthResponse
    {
        Token = token,
        PlayerId = playerId,
        Name = googleUser.Name,
        Email = googleUser.Email,
        Picture = googleUser.Picture
    });
});

// Dev-only: skip Google for local testing
if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/auth/dev", (
        DevLoginRequest request,
        JwtTokenService jwt,
        UserMappingService users) =>
    {
        var name = string.IsNullOrWhiteSpace(request.Name) ? "Dev Player" : request.Name.Trim();
        var devGoogleId = $"dev-{name.ToLowerInvariant().Replace(' ', '-')}";

        var googleUser = new GoogleUser
        {
            GoogleId = devGoogleId,
            Email = $"{devGoogleId}@dev.local",
            Name = name,
            Picture = string.Empty
        };

        var playerId = users.GetOrCreatePlayerId(googleUser);
        var token = jwt.GenerateToken(playerId, googleUser.GoogleId,
            googleUser.Name, googleUser.Email, googleUser.Picture);

        return Results.Ok(new AuthResponse
        {
            Token = token,
            PlayerId = playerId,
            Name = name,
            Email = googleUser.Email,
            Picture = string.Empty
        });
    });
}

app.MapHub<GameHub>("/gamehub");

// Health check for Render keep-alive
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// SPA fallback — serve index.html for Angular routes
app.MapFallbackToFile("index.html");

app.Run();

// Minimal record for dev login
record DevLoginRequest(string Name);
