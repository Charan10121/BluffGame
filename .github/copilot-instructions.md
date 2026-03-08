# Bluff Game - AI Coding Agent Instructions

## Architecture Overview

This is a **real-time multiplayer card game** using SignalR (WebSockets) with a server-authoritative architecture. State lives on the server; clients are thin views.

**Three-layer separation**:
1. **Hub** ([GameHub.cs](../BluffGame.Server/Hubs/GameHub.cs)) - Transport layer, handles SignalR connections
2. **Coordinator** ([GameCoordinator.cs](../BluffGame.Server/Services/GameCoordinator.cs)) - Orchestration: timers, bot triggers, broadcasts
3. **Engine** ([GameEngine.cs](../BluffGame.Server/Game/GameEngine.cs)) - Pure game logic (no I/O, no async)

```
Client (Angular) ←──SignalR──→ GameHub ──→ GameCoordinator ──→ GameEngine
                                    ↓              ↓
                              IRoomManager    BotEngine
```

**Authentication flow**: Google OAuth 2.0 → `/api/auth/google` → server validates ID token → issues app JWT → client passes JWT to SignalR via `accessTokenFactory`. Hub is `[Authorize]`.

## Critical Patterns

### 1. Concurrency & Thread Safety
Every `Room` has a `SemaphoreSlim` for protecting game state mutations:

```csharp
await room.Semaphore.WaitAsync();
try { 
    _engine.PlayCards(room, ...); 
} 
finally { 
    room.Semaphore.Release(); 
}
```

**Always wrap GameEngine calls in this pattern** when mutating room state. RoomManager uses `ConcurrentDictionary` for thread-safe room/session lookups.

### 2. Information Hiding (Security)
Each player receives a **personalized view** of the game state via `PlayerGameView` DTO:
- Players see their own full hand: `view.Hand`
- Opponents' cards are hidden: `PlayerViewDto` only exposes `CardCount`
- Server never sends opponent hands to clients

**When adding new game state**: Always create player-specific projections in `GameCoordinator.BroadcastGameStateAsync()`.

### 3. SignalR Message Flow
- **Client → Server**: Invoke hub methods (`await this.connection.invoke('PlayCards', ...)`)
- **Server → Client**: Call interface methods on `IGameClient` (`await Clients.Group(roomId).GameStateUpdated(view)`)

**Strongly-typed contract**: [IGameClient.cs](../BluffGame.Server/Hubs/IGameClient.cs) defines all server→client events. Angular subscribes via `connection.on('GameStateUpdated', ...)`.

### 4. Bot Orchestration
Bots are `Player` objects with `Type = PlayerType.Bot`. After each state change, `GameCoordinator` checks if current player is a bot and schedules bot action with delay (`Task.Delay(800-2000ms)` for realism).

**Strategy pattern**: [BotEngine.cs](../BluffGame.Server/AI/BotEngine.cs) delegates to difficulty-specific strategies ([EasyStrategy.cs](../BluffGame.Server/AI/EasyStrategy.cs), [MediumStrategy.cs](../BluffGame.Server/AI/MediumStrategy.cs)).

### 5. Timer Management
`GameCoordinator` uses `ConcurrentDictionary<string, CancellationTokenSource>` to track per-room timers:
- **Challenge window**: 10s for other players to challenge
- **Turn timeout**: 30s for current player to act

When phase changes, **always cancel previous timer** via `CancelAllTimers(roomId)` before starting new one.

### 6. Reconnection Strategy
- Players get persistent `PlayerId` (8-char GUID) tied to their Google identity via [UserMappingService.cs](../BluffGame.Server/Auth/UserMappingService.cs)
- Connection drops → player marked `IsConnected = false` but stays in room
- Reconnect within 90s → `AttemptReconnect()` restores session
- [RoomCleanupService.cs](../BluffGame.Server/Services/RoomCleanupService.cs) removes rooms with no connected humans after grace period

### 7. Authentication & Authorization
- **Google OAuth 2.0 + OIDC**: Client uses Google Identity Services, sends ID token to `POST /api/auth/google`
- **JWT tokens**: [JwtTokenService.cs](../BluffGame.Server/Auth/JwtTokenService.cs) issues app JWTs with `playerId`, `google_id`, `name`, `email` claims
- **Hub `[Authorize]`**: All SignalR methods require valid JWT; PlayerId extracted from `ClaimTypes.NameIdentifier`
- **Dev bypass**: `POST /api/auth/dev` (Development env only) for local testing without Google credentials
- **SignalR JWT delivery**: Token passed via query string (`?access_token=...`) — configured in `JwtBearerEvents.OnMessageReceived`

### 8. Rate Limiting
- [HubRateLimitFilter.cs](../BluffGame.Server/Auth/HubRateLimitFilter.cs) implements `IHubFilter` for per-connection rate limiting
- Sliding window: max 30 invocations per 10-second window per connection
- Throws `HubException` on exceeded rate — client sees error notification
- Auto-cleanup on disconnect via `OnDisconnectedAsync`

### 9. Hub Hardening
- Hub is **thin transport only**: extracts identity from JWT claims, delegates all business rules to Coordinator
- No game state validation in Hub — all checks in `GameCoordinator` (host validation, game status, etc.)
- Coordinator returns errors via `SendErrorToPlayer()` instead of throwing from Hub

## Development Workflows

### Run Locally
```bash
# Terminal 1 - Backend
cd BluffGame.Server
dotnet run  # Starts at http://localhost:5000

# Terminal 2 - Frontend  
cd BluffGame.Client
npm install
npm start   # Starts at http://localhost:4200 with proxy to backend
```

### Docker Build (Production)
```bash
docker build -t bluff-game .
docker run -p 10000:10000 bluff-game
```

**Multi-stage Dockerfile**:
1. Builds Angular → `dist/bluff-game-client/browser`
2. Copies to `BluffGame.Server/wwwroot`
3. Publishes .NET with embedded static files

### Key Files
- [proxy.conf.json](../BluffGame.Client/proxy.conf.json): Routes `/gamehub` WebSocket and `/api` to backend
- [render.yaml](../render.yaml): Render.com deployment (Docker, `/health` endpoint)
- [Program.cs](../BluffGame.Server/Program.cs): DI setup, SignalR config, CORS, JWT auth, auth endpoints, SPA fallback
- [Auth/](../BluffGame.Server/Auth/): `GoogleTokenValidator`, `JwtTokenService`, `UserMappingService`, `HubRateLimitFilter`

## Code Conventions

### C# Backend
- **Services**: Singleton (`IRoomManager`, `IGameCoordinator`, `GameEngine`, `BotEngine`) - handle concurrency manually
- **HostedService**: [RoomCleanupService.cs](../BluffGame.Server/Services/RoomCleanupService.cs) runs every 2 minutes
- **DTOs**: Suffix `Dto` or live in `Models.DTOs` namespace ([DTOs.cs](../BluffGame.Server/Models/DTOs.cs))
- **Enums**: [Enums.cs](../BluffGame.Server/Models/Enums.cs) for `RoomStatus`, `TurnPhase`, `PlayerType`, `BotDifficulty`

### TypeScript Frontend
- **Services**: `SignalRService` (low-level connection) → `GameService` (business logic + RxJS state)
- **Models**: [models/index.ts](../BluffGame.Client/src/app/models/index.ts) mirrors server DTOs
- **State**: RxJS `BehaviorSubject` for reactive state management

## Common Pitfalls

1. **Don't mutate room state outside semaphore** → race conditions
2. **Don't send raw `Room` or `Player` objects to client** → exposes all hands. Always use DTOs.
3. **Don't forget to cancel timers** → memory leaks, phantom callbacks
4. **CORS in dev**: `UseCors("Development")` only applies in `Development` environment ([Program.cs](../BluffGame.Server/Program.cs#L38))
5. **Reconnection**: Client stores `playerId` in localStorage ([signalr.service.ts](../BluffGame.Client/src/app/services/signalr.service.ts))
6. **JWT in SignalR**: WebSockets can't use HTTP headers — token must be sent via `?access_token` query string, handled by `OnMessageReceived` event
7. **Never trust client-supplied PlayerId**: Always extract from JWT `ClaimTypes.NameIdentifier` in hub methods
8. **Google Client ID**: Set `GOOGLE_CLIENT_ID` env var and update `environment.ts` `googleClientId` before deploying

## Extending the Game

### Adding a new client event
1. Add method to `IGameClient` interface
2. Call it from `GameCoordinator` (e.g., `await _hub.Clients.Group(roomId).YourEvent(...)`)
3. Subscribe in Angular: `connection.on('YourEvent', (data) => { ... })`

### Adding a new bot strategy
1. Implement `IBotStrategy` interface
2. Register in `BotEngine._strategies` dictionary
3. Add enum value to `BotDifficulty`

### Scaling considerations
- **In-memory state doesn't survive restarts** - consider Redis or event sourcing for production
- **SignalR backplane** (Redis) needed for multiple server instances - sticky sessions don't work with WebSockets
- **Room cleanup** prevents unbounded memory growth; tune timeouts in [RoomCleanupService.cs](../BluffGame.Server/Services/RoomCleanupService.cs#L14-L17)
