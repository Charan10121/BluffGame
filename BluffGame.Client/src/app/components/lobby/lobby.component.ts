import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { GameService } from '../../services/game.service';
import { CreateRoomRequest } from '../../models';

@Component({
  selector: 'app-lobby',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './lobby.component.html',
  styleUrl: './lobby.component.scss'
})
export class LobbyComponent {
  gameService = inject(GameService);

  // Name entry
  nameInput = '';

  // Create room form
  showCreateForm = signal(false);
  roomName = '';
  maxPlayers = 4;
  botCount = 1;
  botDifficulty = 'Easy';

  get needsName(): boolean {
    return !this.gameService.playerName();
  }

  async submitName(): Promise<void> {
    if (!this.nameInput.trim()) return;
    await this.gameService.setName(this.nameInput.trim());
  }

  toggleCreateForm(): void {
    this.showCreateForm.update(v => !v);
  }

  async createRoom(): Promise<void> {
    const request: CreateRoomRequest = {
      roomName: this.roomName || `${this.gameService.playerName()}'s Room`,
      playerName: this.gameService.playerName()!,
      maxPlayers: this.maxPlayers,
      botCount: this.botCount,
      botDifficulty: this.botDifficulty
    };
    await this.gameService.createRoom(request);
    this.showCreateForm.set(false);
  }

  async joinRoom(roomId: string): Promise<void> {
    await this.gameService.joinRoom(roomId);
  }

  async refresh(): Promise<void> {
    await this.gameService.refreshRooms();
  }

  get maxBots(): number {
    return this.maxPlayers - 1;
  }

  onMaxPlayersChange(): void {
    if (this.botCount >= this.maxPlayers) {
      this.botCount = this.maxPlayers - 1;
    }
  }
}
