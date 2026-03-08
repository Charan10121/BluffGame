import { Injectable, inject, signal, computed } from '@angular/core';
import { Router } from '@angular/router';
import { SignalRService } from './signalr.service';
import { AuthService } from './auth.service';
import {
  PlayerGameView,
  RoomSummary,
  RoomDetails,
  CreateRoomRequest
} from '../models';

const STORAGE_PLAYER_ID = 'bluff_player_id';
const STORAGE_PLAYER_NAME = 'bluff_player_name';
const STORAGE_ROOM_ID = 'bluff_room_id';
const MAX_NOTIFICATIONS = 6;
const NOTIFICATION_TTL_MS = 5000;

/**
 * High-level game service.
 * Manages player identity, room state, game state, and UI-facing signals.
 */
@Injectable({ providedIn: 'root' })
export class GameService {
  private signalR = inject(SignalRService);
  private authService = inject(AuthService);
  private router = inject(Router);

  // ── Identity ──────────────────────────────────────────────────────

  playerId = signal<string | null>(null);

  // ── State ─────────────────────────────────────────────────────────

  rooms = signal<RoomSummary[]>([]);
  currentRoom = signal<RoomDetails | null>(null);
  gameState = signal<PlayerGameView | null>(null);
  isConnected = signal(false);
  challengeTimeRemaining = signal(0);
  turnTimeRemaining = signal(0);
  notifications = signal<string[]>([]);
  gameOverInfo = signal<{ winnerId: string; winnerName: string } | null>(null);

  // ── Reconnect prompt ──────────────────────────────────────────────

  showReconnectPrompt = signal(false);
  pendingReconnectData = signal<{ room: RoomDetails; gameState: PlayerGameView | null } | null>(null);

  // ── Computed ──────────────────────────────────────────────────────

  isMyTurn = computed(() => {
    const state = this.gameState();
    return state?.currentPlayerId === this.playerId();
  });

  isHost = computed(() => {
    const room = this.currentRoom();
    return room?.hostPlayerId === this.playerId();
  });

  canChallenge = computed(() => {
    const state = this.gameState();
    return (
      state?.phase === 'ChallengeWindow' &&
      state?.lastClaim?.playerId !== this.playerId() &&
      this.challengeTimeRemaining() > 0
    );
  });

  isPlaying = computed(() => {
    const room = this.currentRoom();
    return room?.status === 'Playing';
  });

  isWaiting = computed(() => {
    const room = this.currentRoom();
    return room?.status === 'Waiting';
  });

  isFinished = computed(() => {
    const room = this.currentRoom();
    return room?.status === 'Finished';
  });

  didIWin = computed(() => {
    return this.gameOverInfo()?.winnerId === this.playerId();
  });

  // ── Timers ────────────────────────────────────────────────────────

  private challengeInterval: ReturnType<typeof setInterval> | null = null;
  private turnInterval: ReturnType<typeof setInterval> | null = null;

  // ── Initialisation ────────────────────────────────────────────────

  async initialize(): Promise<void> {
    this.setupSubscriptions();
    await this.connect();
  }

  private async connect(): Promise<void> {
    try {
      // Only connect if authenticated
      if (!this.authService.isAuthenticated()) {
        return;
      }

      await this.signalR.start();
      this.isConnected.set(true);

      // Use auth-provided identity
      const user = this.authService.user();
      const storedId = localStorage.getItem(STORAGE_PLAYER_ID);
      const name = user?.name || localStorage.getItem(STORAGE_PLAYER_NAME) || 'Player';

      if (storedId) {
        const result = await this.signalR.attemptReconnect(storedId, name);

        // Always use the server-returned playerId (may be new if session expired)
        const activePlayerId = result.playerId || storedId;
        this.playerId.set(activePlayerId);
        localStorage.setItem(STORAGE_PLAYER_ID, activePlayerId);
        localStorage.setItem(STORAGE_PLAYER_NAME, name);

        if (result.success && result.room) {
          // Show reconnect prompt instead of auto-navigating
          this.pendingReconnectData.set({
            room: result.room,
            gameState: result.gameState
          });
          this.showReconnectPrompt.set(true);
          return;
        }

        // No active room — clear stored room ID
        localStorage.removeItem(STORAGE_ROOM_ID);
        return;
      }

      // No stored session — register with auth identity
      if (user) {
        const id = await this.signalR.setPlayerInfo(user.name);
        this.playerId.set(id);
        localStorage.setItem(STORAGE_PLAYER_ID, id);
        localStorage.setItem(STORAGE_PLAYER_NAME, user.name);
      }
    } catch (err) {
      console.error('Connection failed:', err);
      // Retry after delay
      setTimeout(() => this.connect(), 3000);
    }
  }

  // ── Actions ───────────────────────────────────────────────────────

  async setName(name: string): Promise<void> {
    const id = await this.signalR.setPlayerInfo(name);
    this.playerId.set(id);
    localStorage.setItem(STORAGE_PLAYER_ID, id);
    localStorage.setItem(STORAGE_PLAYER_NAME, name);
    await this.signalR.getRooms();
  }

  async refreshRooms(): Promise<void> {
    await this.signalR.getRooms();
  }

  async createRoom(request: CreateRoomRequest): Promise<void> {
    await this.signalR.createRoom(request);
  }

  async joinRoom(roomId: string): Promise<void> {
    await this.signalR.joinRoom(roomId);
  }

  async leaveRoom(): Promise<void> {
    await this.signalR.leaveRoom();
    this.currentRoom.set(null);
    this.gameState.set(null);
    this.gameOverInfo.set(null);
    this.clearTimers();
    localStorage.removeItem(STORAGE_ROOM_ID);
    this.router.navigate(['/lobby']);
  }

  async startGame(): Promise<void> {
    await this.signalR.startGame();
  }

  async playCards(cardIndices: number[], claimedRank: string): Promise<void> {
    await this.signalR.playCards({ cardIndices, claimedRank });
  }

  async challenge(): Promise<void> {
    await this.signalR.challenge();
  }

  async pass(): Promise<void> {
    await this.signalR.pass();
  }

  // ── Reconnect prompt ──────────────────────────────────────────────

  async acceptReconnect(): Promise<void> {
    const data = this.pendingReconnectData();
    if (!data) return;

    this.currentRoom.set(data.room);
    if (data.gameState) {
      this.gameState.set(data.gameState);
    }
    this.showReconnectPrompt.set(false);
    this.pendingReconnectData.set(null);
    localStorage.setItem(STORAGE_ROOM_ID, data.room.id);
    this.router.navigate(['/room', data.room.id]);
  }

  async declineReconnect(): Promise<void> {
    this.showReconnectPrompt.set(false);
    this.pendingReconnectData.set(null);
    localStorage.removeItem(STORAGE_ROOM_ID);
    try {
      await this.signalR.leaveRoom();
    } catch { /* may fail if not in room server-side */ }
    this.currentRoom.set(null);
    this.gameState.set(null);
    this.router.navigate(['/lobby']);
  }

  // ── Subscriptions ─────────────────────────────────────────────────

  private setupSubscriptions(): void {
    this.signalR.connectionState$.subscribe(state => {
      this.isConnected.set(state === 'Connected' as any);
    });

    this.signalR.onRoomList$.subscribe(rooms => {
      this.rooms.set(rooms);
    });

    this.signalR.onRoomJoined$.subscribe(details => {
      this.currentRoom.set(details);
      this.gameOverInfo.set(null);
      localStorage.setItem(STORAGE_ROOM_ID, details.id);
      this.router.navigate(['/room', details.id]);
    });

    this.signalR.onRoomUpdated$.subscribe(details => {
      this.currentRoom.set(details);
    });

    this.signalR.onGameStateUpdated$.subscribe(view => {
      this.gameState.set(view);
      // Also update room status from game state
      const room = this.currentRoom();
      if (room && view.winnerId) {
        this.currentRoom.set({ ...room, status: 'Finished' });
      } else if (room && room.status !== 'Playing' && view.phase) {
        this.currentRoom.set({ ...room, status: 'Playing' });
      }
    });

    this.signalR.onChallengeWindowStarted$.subscribe(seconds => {
      this.startChallengeCountdown(seconds);
    });

    this.signalR.onTurnTimerStarted$.subscribe(seconds => {
      this.startTurnCountdown(seconds);
    });

    this.signalR.onGameOver$.subscribe(info => {
      this.gameOverInfo.set(info);
      this.clearTimers();
      const room = this.currentRoom();
      if (room) {
        this.currentRoom.set({ ...room, status: 'Finished' });
      }
    });

    this.signalR.onError$.subscribe(message => {
      this.addNotification(`❌ ${message}`);
    });

    this.signalR.onNotification$.subscribe(message => {
      this.addNotification(message);
    });
  }

  // ── Timer helpers ─────────────────────────────────────────────────

  private startChallengeCountdown(seconds: number): void {
    this.clearChallengeTimer();
    this.challengeTimeRemaining.set(seconds);
    this.challengeInterval = setInterval(() => {
      const current = this.challengeTimeRemaining();
      if (current <= 0) {
        this.clearChallengeTimer();
      } else {
        this.challengeTimeRemaining.set(current - 1);
      }
    }, 1000);
  }

  private startTurnCountdown(seconds: number): void {
    this.clearTurnTimer();
    this.turnTimeRemaining.set(seconds);
    this.turnInterval = setInterval(() => {
      const current = this.turnTimeRemaining();
      if (current <= 0) {
        this.clearTurnTimer();
      } else {
        this.turnTimeRemaining.set(current - 1);
      }
    }, 1000);
  }

  private clearChallengeTimer(): void {
    if (this.challengeInterval) {
      clearInterval(this.challengeInterval);
      this.challengeInterval = null;
    }
    this.challengeTimeRemaining.set(0);
  }

  private clearTurnTimer(): void {
    if (this.turnInterval) {
      clearInterval(this.turnInterval);
      this.turnInterval = null;
    }
    this.turnTimeRemaining.set(0);
  }

  private clearTimers(): void {
    this.clearChallengeTimer();
    this.clearTurnTimer();
  }

  // ── Notifications ─────────────────────────────────────────────────

  private addNotification(message: string): void {
    const current = this.notifications();
    const updated = [...current, message].slice(-MAX_NOTIFICATIONS);
    this.notifications.set(updated);

    // Auto-remove after TTL
    setTimeout(() => {
      const now = this.notifications();
      const idx = now.indexOf(message);
      if (idx >= 0) {
        this.notifications.set(now.filter((_, i) => i !== idx));
      }
    }, NOTIFICATION_TTL_MS);
  }
}
