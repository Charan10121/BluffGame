import { Injectable, inject, signal, computed } from '@angular/core';
import { Router } from '@angular/router';
import { SignalRService } from './signalr.service';
import {
  PlayerGameView,
  RoomSummary,
  RoomDetails,
  CreateRoomRequest
} from '../models';

const STORAGE_PLAYER_ID = 'bluff_player_id';
const STORAGE_PLAYER_NAME = 'bluff_player_name';
const MAX_NOTIFICATIONS = 6;
const NOTIFICATION_TTL_MS = 5000;

/**
 * High-level game service.
 * Manages player identity, room state, game state, and UI-facing signals.
 */
@Injectable({ providedIn: 'root' })
export class GameService {
  private signalR = inject(SignalRService);
  private router = inject(Router);

  // ── Identity ──────────────────────────────────────────────────────

  playerId = signal<string | null>(null);
  playerName = signal<string | null>(null);

  // ── State ─────────────────────────────────────────────────────────

  rooms = signal<RoomSummary[]>([]);
  currentRoom = signal<RoomDetails | null>(null);
  gameState = signal<PlayerGameView | null>(null);
  isConnected = signal(false);
  challengeTimeRemaining = signal(0);
  turnTimeRemaining = signal(0);
  notifications = signal<string[]>([]);
  gameOverInfo = signal<{ winnerId: string; winnerName: string } | null>(null);

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
      await this.signalR.start();
      this.isConnected.set(true);

      // Attempt reconnection with stored identity
      const storedId = localStorage.getItem(STORAGE_PLAYER_ID);
      const storedName = localStorage.getItem(STORAGE_PLAYER_NAME);

      if (storedId && storedName) {
        const result = await this.signalR.attemptReconnect(storedId, storedName);
        this.playerId.set(storedId);
        this.playerName.set(storedName);

        if (result.success && result.room) {
          this.currentRoom.set(result.room);
          if (result.gameState) {
            this.gameState.set(result.gameState);
          }
          this.router.navigate(['/room', result.room.id]);
          return;
        }
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
    this.playerName.set(name);
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
