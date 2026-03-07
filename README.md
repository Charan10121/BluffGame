# 🃏 Bluff – Multiplayer Card Game

A real-time multiplayer implementation of the classic card game **Bluff** (also known as *Cheat* or *BS*), built with **ASP.NET Core + SignalR** backend and **Angular** frontend.

## Features

- **Real-time multiplayer** via WebSockets (SignalR)
- **AI bots** with Easy and Medium difficulty strategies
- **Lobby system** – create rooms, share with friends, or fill with bots
- **Hybrid play** – any mix of human players and bots (2–6 players)
- **Reconnection handling** – rejoin games after disconnection
- **Server-authoritative** – all game logic runs on the server to prevent cheating

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Frontend | Angular 18, TypeScript, SCSS |
| Backend | ASP.NET Core 8, C#, SignalR |
| Real-time | SignalR (WebSocket + fallback) |
| Hosting | Render.com (Docker, free tier) |

## Game Rules

1. A standard 52-card deck is dealt evenly among all players
2. On your turn, place 1–4 cards face-down and **claim** a rank (e.g., "2 Kings")
3. Other players have a window to **challenge** ("Bluff!") your claim
4. If challenged:
   - **Was a bluff** → you pick up the entire pile
   - **Was truthful** → the challenger picks up the pile
5. First player to empty their hand **wins**

## Local Development

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/)
- [Angular CLI](https://angular.dev/) (`npm i -g @angular/cli`)

### Run the backend

```bash
cd BluffGame.Server
dotnet run
```

Server starts at `http://localhost:5000`.

### Run the frontend (dev server)

```bash
cd BluffGame.Client
npm install
npm start
```

Angular dev server starts at `http://localhost:4200` with proxy to the backend.

### Docker (full build)

```bash
docker build -t bluff-game .
docker run -p 10000:10000 bluff-game
```

Open `http://localhost:10000`.

## Deployment

This project is configured for one-click deployment on **Render.com**:

1. Push to a GitHub repository
2. Connect the repo on [Render Dashboard](https://dashboard.render.com/)
3. Render auto-detects `render.yaml` and deploys

### Keep-Alive (prevent free-tier sleep)

Use [cron-job.org](https://cron-job.org) to ping `https://<your-app>.onrender.com/health` every 14 minutes.

## Project Structure

```
BluffGame/
├── BluffGame.Server/          # ASP.NET Core backend
│   ├── Models/                # Domain models & DTOs
│   ├── Game/                  # Game engine (deck, rules)
│   ├── AI/                    # Bot strategies
│   ├── Services/              # Room manager, game coordinator
│   └── Hubs/                  # SignalR hub
├── BluffGame.Client/          # Angular frontend
│   └── src/app/
│       ├── models/            # TypeScript interfaces
│       ├── services/          # SignalR & game services
│       └── components/        # UI components
├── Dockerfile                 # Multi-stage build
└── render.yaml                # Render deployment config
```

## License

MIT
