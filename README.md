
# 🃏 Bluff — Real-Time Multiplayer Card Game

[Live Demo](https://bluff-the-card-game-fe5n.onrender.com/)

A real-time multiplayer implementation of the classic card game **Bluff** (also known as *Cheat* or *BS*), built with **ASP.NET Core + SignalR** and **Angular**.

Play with friends or against AI bots — bluff your way to victory!

## ✨ Features

- **Real-time multiplayer** — WebSocket-powered via SignalR with automatic reconnection
- **AI bots** — Easy and Medium difficulty strategies with humanised play delays
- **Google Sign-In** — OAuth 2.0 authentication (dev login available for local testing)
- **Lobby system** — create rooms, set player counts, add bots, share room codes
- **Hybrid play** — any mix of human players and bots (2–6 players per room)
- **Reconnection handling** — rejoin in-progress games after disconnection
- **Server-authoritative** — all game logic runs server-side to prevent cheating
- **Turn timers** — 30-second turn timeout with auto-pass
- **Responsive dark-themed UI** — casino-style green table design

## 🎮 Game Rules

1. A **54-card deck** (standard 52 + 2 Jokers) is dealt evenly among all players
2. On your turn, select any number of cards, place them face-down, and **claim** a rank (e.g. "3 Kings")
3. **Round lock** — once a rank is claimed, all subsequent plays in that round must claim the same rank (or pass)
4. **Jokers are wild** — they always count as matching the claimed rank
5. After each play, other players have a **10-second window** to call **"Bluff!"**
6. If challenged:
   - **Was a bluff** → the player who played picks up the entire pile
   - **Was truthful** → the challenger picks up the pile
   - The challenge winner starts the next round
7. If everyone passes, the pile is cleared and a new round begins
8. **First player to empty their hand wins!**

## 🛠 Tech Stack

| Layer | Technology |
|-------|-----------|
| Frontend | Angular 18, TypeScript, SCSS |
| Backend | ASP.NET Core 8, C#, SignalR |
| Auth | Google OAuth 2.0, JWT |
| Deployment | Docker, Render.com |

## 🚀 Local Development

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/)

### Run

```bash
# Terminal 1 — Backend
cd BluffGame.Server
dotnet run
# → http://localhost:5000

# Terminal 2 — Frontend
cd BluffGame.Client
npm install
npm start
# → http://localhost:4200 (proxied to backend)
```

In development mode a **Dev Login** is available — no Google credentials required.

### Docker

```bash
docker build -t bluff-game .
docker run -p 10000:10000 bluff-game
# → http://localhost:10000
```

### Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `GOOGLE_CLIENT_ID` | Production | Google OAuth 2.0 client ID |
| `JWT_SECRET` | Production | Signing key for app JWTs (min 32 chars) |
| `ASPNETCORE_ENVIRONMENT` | — | Set to `Production` for deployed builds |

> A dev fallback JWT secret is used automatically in Development mode.

## 🌐 Deployment

Configured for one-click deployment on [Render.com](https://render.com):

1. Push to GitHub
2. Connect the repo on the [Render Dashboard](https://dashboard.render.com/)
3. Set `GOOGLE_CLIENT_ID` and `JWT_SECRET` environment variables
4. Render auto-detects `render.yaml` and deploys

**Keep-alive** (free tier): use [cron-job.org](https://cron-job.org) to ping `https://<your-app>.onrender.com/health` every 14 minutes.

## 📁 Project Structure

```
BluffGame/
├── BluffGame.Server/           # ASP.NET Core backend
│   ├── AI/                     # Bot strategies (Easy, Medium)
│   ├── Auth/                   # Google OAuth, JWT, rate limiting
│   ├── Game/                   # Game engine & deck logic
│   ├── Hubs/                   # SignalR hub & client contract
│   ├── Models/                 # Domain models, DTOs, enums
│   └── Services/               # Room manager, game coordinator, cleanup
├── BluffGame.Client/           # Angular 18 frontend
│   └── src/app/
│       ├── components/         # Login, Lobby, Room, Game Board, Card
│       ├── models/             # TypeScript interfaces
│       └── services/           # Auth, SignalR, Game state
├── Dockerfile                  # Multi-stage build (Node → .NET → runtime)
└── render.yaml                 # Render.com deployment config
```
