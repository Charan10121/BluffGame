# 🃏 Bluff – Real-Time Multiplayer Card Game

A real-time multiplayer implementation of the classic card game **Bluff** (also known as *Cheat* or *BS*), built with **ASP.NET Core + SignalR** backend and **Angular** frontend.
**Live Demo:** https://bluff-the-card-game-fe5n.onrender.com/


## Features

- **Real-time multiplayer** via WebSockets (SignalR)
- **AI bots** with Easy and Medium difficulty strategies
- **Lobby system** – create rooms, share with friends, or fill with bots
- **Google OAuth 2.0** authentication with JWT-secured connections
- **Reconnection handling** – rejoin games after brief disconnections
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
2. On your turn, place 1–4 cards face-down and claim a rank (e.g., "2 Kings")
1. A standard 52-card deck is dealt evenly among all players
2. On your turn, place 1–4 cards face-down and **claim** a rank (e.g., "2 Kings")
3. Other players have a window to **challenge** ("Bluff!") your claim
4. If challenged:
   - **Was a bluff** → you pick up the entire pile
   - **Was truthful** → the challenger picks up the pile
5. First player to empty their hand **wins**

**Requirements:** .NET 8 SDK, Node.js 20+

**Setup environment variables:**
```bash
# BluffGame.Server
export GOOGLE_CLIENT_ID="your-oauth-client-id"
export JWT_SECRET="your-secret-key-min-32-chars"
```
- [Node.js 20+](https://nodejs.org/)
**Backend:**

### Run the backend

```bash
cd BluffGame.Server
dotnet run
Runs at http://localhost:5000

**Frontend:**

### Run the frontend (dev server)

```bash
cd BluffGame.Client
npm install
npm start
Runs at http://localhost:4200 (proxies `/api` and `/gamehub` to backend)

Angular dev server starts at `http://localhost:4200` with proxy to the backend.

### Docker (full build)

```bash
docker build -t bluff-game .
docker run -p 10000:10000 bluff-game
Runs at http://localhost:10000

Open `http://localhost:10000`.

Push to GitHub and connect on [Render Dashboard](https://dashboard.render.com/). Auto-detects `render.yaml` and deploys via Docker.


MIT
