import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject, BehaviorSubject } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  PlayerGameView,
  RoomSummary,
  RoomDetails,
  ReconnectResult,
  CreateRoomRequest,
  PlayCardsRequest
} from '../models';

/**
 * Low-level SignalR connection wrapper.
 * Exposes hub events as RxJS Subjects and hub invocations as async methods.
 */
@Injectable({ providedIn: 'root' })
export class SignalRService {

  private connection: signalR.HubConnection;

  // ── Connection state ──────────────────────────────────────────────

  connectionState$ = new BehaviorSubject<signalR.HubConnectionState>(
    signalR.HubConnectionState.Disconnected
  );

  // ── Server → Client events ────────────────────────────────────────

  onPlayerIdAssigned$ = new Subject<string>();
  onRoomList$ = new Subject<RoomSummary[]>();
  onRoomJoined$ = new Subject<RoomDetails>();
  onRoomUpdated$ = new Subject<RoomDetails>();
  onGameStateUpdated$ = new Subject<PlayerGameView>();
  onChallengeWindowStarted$ = new Subject<number>();
  onTurnTimerStarted$ = new Subject<number>();
  onGameOver$ = new Subject<{ winnerId: string; winnerName: string }>();
  onError$ = new Subject<string>();
  onNotification$ = new Subject<string>();
  onPlayerDisconnected$ = new Subject<string>();
  onPlayerReconnected$ = new Subject<string>();

  constructor() {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(environment.hubUrl)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.registerLifecycleHandlers();
    this.registerEventHandlers();
  }

  // ── Connection management ─────────────────────────────────────────

  async start(): Promise<void> {
    if (this.connection.state === signalR.HubConnectionState.Disconnected) {
      await this.connection.start();
      this.connectionState$.next(this.connection.state);
    }
  }

  // ── Hub invocations (Client → Server) ─────────────────────────────

  async setPlayerInfo(name: string): Promise<string> {
    return await this.connection.invoke<string>('SetPlayerInfo', name);
  }

  async attemptReconnect(playerId: string, name: string): Promise<ReconnectResult> {
    return await this.connection.invoke<ReconnectResult>('AttemptReconnect', playerId, name);
  }

  async getRooms(): Promise<void> {
    await this.connection.invoke('GetRooms');
  }

  async createRoom(request: CreateRoomRequest): Promise<void> {
    await this.connection.invoke('CreateRoom', request);
  }

  async joinRoom(roomId: string): Promise<void> {
    await this.connection.invoke('JoinRoom', roomId);
  }

  async leaveRoom(): Promise<void> {
    await this.connection.invoke('LeaveRoom');
  }

  async startGame(): Promise<void> {
    await this.connection.invoke('StartGame');
  }

  async playCards(request: PlayCardsRequest): Promise<void> {
    await this.connection.invoke('PlayCards', request);
  }

  async challenge(): Promise<void> {
    await this.connection.invoke('Challenge');
  }

  // ── Private ───────────────────────────────────────────────────────

  private registerLifecycleHandlers(): void {
    this.connection.onreconnecting(() => {
      this.connectionState$.next(signalR.HubConnectionState.Reconnecting);
    });

    this.connection.onreconnected(() => {
      this.connectionState$.next(signalR.HubConnectionState.Connected);
    });

    this.connection.onclose(() => {
      this.connectionState$.next(signalR.HubConnectionState.Disconnected);
    });
  }

  private registerEventHandlers(): void {
    this.connection.on('PlayerIdAssigned', (id: string) =>
      this.onPlayerIdAssigned$.next(id));

    this.connection.on('RoomList', (rooms: RoomSummary[]) =>
      this.onRoomList$.next(rooms));

    this.connection.on('RoomJoined', (details: RoomDetails) =>
      this.onRoomJoined$.next(details));

    this.connection.on('RoomUpdated', (details: RoomDetails) =>
      this.onRoomUpdated$.next(details));

    this.connection.on('GameStateUpdated', (view: PlayerGameView) =>
      this.onGameStateUpdated$.next(view));

    this.connection.on('ChallengeWindowStarted', (seconds: number) =>
      this.onChallengeWindowStarted$.next(seconds));

    this.connection.on('TurnTimerStarted', (seconds: number) =>
      this.onTurnTimerStarted$.next(seconds));

    this.connection.on('GameOver', (winnerId: string, winnerName: string) =>
      this.onGameOver$.next({ winnerId, winnerName }));

    this.connection.on('Error', (message: string) =>
      this.onError$.next(message));

    this.connection.on('Notification', (message: string) =>
      this.onNotification$.next(message));

    this.connection.on('PlayerDisconnected', (name: string) =>
      this.onPlayerDisconnected$.next(name));

    this.connection.on('PlayerReconnected', (name: string) =>
      this.onPlayerReconnected$.next(name));
  }
}
