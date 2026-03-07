import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { GameService } from '../../services/game.service';

@Component({
  selector: 'app-waiting-room',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './waiting-room.component.html',
  styleUrl: './waiting-room.component.scss'
})
export class WaitingRoomComponent {
  gameService = inject(GameService);

  get room() {
    return this.gameService.currentRoom();
  }

  get canStart(): boolean {
    const room = this.room;
    if (!room) return false;
    return this.gameService.isHost() && room.players.length >= 2;
  }

  async startGame(): Promise<void> {
    await this.gameService.startGame();
  }

  async leaveRoom(): Promise<void> {
    await this.gameService.leaveRoom();
  }

  copyRoomCode(): void {
    const room = this.room;
    if (room) {
      navigator.clipboard.writeText(room.id);
    }
  }
}
