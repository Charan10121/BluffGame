import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { GameService } from '../../services/game.service';
import { CardComponent } from '../card/card.component';
import { RANKS } from '../../models';

@Component({
  selector: 'app-game-board',
  standalone: true,
  imports: [CommonModule, FormsModule, CardComponent],
  templateUrl: './game-board.component.html',
  styleUrl: './game-board.component.scss'
})
export class GameBoardComponent {
  gameService = inject(GameService);

  selectedIndices = signal<Set<number>>(new Set());
  selectedRank = signal<string>('');
  showExitConfirm = signal(false);
  ranks = RANKS;
  Math = Math; // expose to template

  // ── Computed ──────────────────────────────────────────────────────

  opponents = computed(() => {
    const state = this.gameState();
    if (!state) return [];
    return state.players.filter(p => p.id !== this.gameService.playerId());
  });

  get gameState() {
    return this.gameService.gameState;
  }

  currentPlayerName = computed(() => {
    const state = this.gameState();
    if (!state?.currentPlayerId) return '';
    return state.players.find(p => p.id === state.currentPlayerId)?.name ?? '';
  });

  canPlay = computed(() => {
    const state = this.gameState();
    const effectiveRank = state?.roundClaimedRank ?? this.selectedRank();

    return (
      this.gameService.isMyTurn() &&
      state?.phase === 'PlayCards' &&
      this.selectedIndices().size > 0 &&
      this.selectedIndices().size <= 4 &&
      !!effectiveRank
    );
  });

  canPass = computed(() => {
    return (
      this.gameService.isMyTurn() &&
      this.gameState()?.phase === 'PlayCards'
    );
  });

  // ── Card selection ────────────────────────────────────────────────

  toggleCard(index: number): void {
    if (this.gameState()?.phase !== 'PlayCards' || !this.gameService.isMyTurn()) return;

    const current = new Set(this.selectedIndices());
    if (current.has(index)) {
      current.delete(index);
    } else if (current.size < 4) {
      current.add(index);
    }
    this.selectedIndices.set(current);
  }

  isSelected(index: number): boolean {
    return this.selectedIndices().has(index);
  }

  // ── Actions ───────────────────────────────────────────────────────

  async play(): Promise<void> {
    if (!this.canPlay()) return;

    const state = this.gameState();
    const effectiveRank = state?.roundClaimedRank ?? this.selectedRank();
    if (!effectiveRank) return;

    const indices = [...this.selectedIndices()];
    this.selectedIndices.set(new Set());
    if (!state?.roundClaimedRank) {
      this.selectedRank.set('');
    }
    await this.gameService.playCards(indices, effectiveRank);
  }

  async challenge(): Promise<void> {
    if (!this.gameService.canChallenge()) return;
    await this.gameService.challenge();
  }

  async pass(): Promise<void> {
    if (!this.canPass()) return;
    await this.gameService.pass();
  }

  confirmExit(): void {
    this.showExitConfirm.set(true);
  }

  cancelExit(): void {
    this.showExitConfirm.set(false);
  }

  async leaveRoom(): Promise<void> {
    this.showExitConfirm.set(false);
    await this.gameService.leaveRoom();
  }

  // ── Helpers ───────────────────────────────────────────────────────

  isCurrentPlayer(playerId: string): boolean {
    return this.gameState()?.currentPlayerId === playerId;
  }
}
