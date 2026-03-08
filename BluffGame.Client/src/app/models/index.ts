// ── Card ──────────────────────────────────────────────────────────────

export interface CardDto {
  suit: string;
  rank: string;
  display: string;
  suitSymbol: string;
  rankSymbol: string;
  isRed: boolean;
}

// ── Player ───────────────────────────────────────────────────────────

export interface PlayerViewDto {
  id: string;
  name: string;
  cardCount: number;
  isBot: boolean;
  isConnected: boolean;
  hasWon: boolean;
}

// ── Game state (personalised per player) ─────────────────────────────

export interface PlayerGameView {
  roomId: string;
  hand: CardDto[];
  pileCount: number;
  roundClaimedRank: string | null;
  lastClaim: ClaimDto | null;
  players: PlayerViewDto[];
  currentPlayerId: string | null;
  phase: string;
  turnNumber: number;
  winnerId: string | null;
  lastChallengeResult: ChallengeResultDto | null;
}

export interface ClaimDto {
  playerId: string;
  playerName: string;
  claimedRank: string;
  cardCount: number;
}

export interface ChallengeResultDto {
  challengerId: string;
  challengerName: string;
  challengedPlayerId: string;
  challengedPlayerName: string;
  wasBluff: boolean;
  loserId: string;
  loserName: string;
  revealedCards: CardDto[];
  pileSize: number;
}

// ── Room ─────────────────────────────────────────────────────────────

export interface RoomSummary {
  id: string;
  name: string;
  playerCount: number;
  maxPlayers: number;
  botCount: number;
  status: string;
  hostName: string;
}

export interface RoomDetails {
  id: string;
  name: string;
  hostPlayerId: string;
  players: PlayerViewDto[];
  settings: RoomSettingsDto;
  status: string;
}

export interface RoomSettingsDto {
  maxPlayers: number;
  botCount: number;
  botDifficulty: string;
  turnTimeoutSeconds: number;
  challengeWindowSeconds: number;
}

// ── Requests ─────────────────────────────────────────────────────────

export interface CreateRoomRequest {
  roomName: string;
  playerName: string;
  maxPlayers: number;
  botCount: number;
  botDifficulty: string;
}

export interface PlayCardsRequest {
  cardIndices: number[];
  claimedRank: string;
}

// ── Reconnect ────────────────────────────────────────────────────────

export interface ReconnectResult {
  success: boolean;
  playerId: string | null;
  room: RoomDetails | null;
  gameState: PlayerGameView | null;
}

// ── Constants ────────────────────────────────────────────────────────

export const RANKS: string[] = [
  'Ace', 'Two', 'Three', 'Four', 'Five', 'Six', 'Seven',
  'Eight', 'Nine', 'Ten', 'Jack', 'Queen', 'King'
];

export const RANK_SYMBOLS: Record<string, string> = {
  Ace: 'A', Two: '2', Three: '3', Four: '4', Five: '5',
  Six: '6', Seven: '7', Eight: '8', Nine: '9', Ten: '10',
  Jack: 'J', Queen: 'Q', King: 'K'
};
