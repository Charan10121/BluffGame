import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { GameService } from '../../services/game.service';
import { WaitingRoomComponent } from '../waiting-room/waiting-room.component';
import { GameBoardComponent } from '../game-board/game-board.component';

@Component({
  selector: 'app-room',
  standalone: true,
  imports: [CommonModule, WaitingRoomComponent, GameBoardComponent],
  template: `
    @if (gameService.isWaiting()) {
      <app-waiting-room />
    } @else if (gameService.isPlaying() || gameService.isFinished()) {
      <app-game-board />
    } @else {
      <div class="loading">
        <p>Loading room...</p>
      </div>
    }
  `,
  styles: [`
    .loading {
      display: flex;
      align-items: center;
      justify-content: center;
      height: 80vh;
      color: var(--text-muted);
      font-size: 1.1rem;
    }
  `]
})
export class RoomComponent implements OnInit {
  gameService = inject(GameService);
  private route = inject(ActivatedRoute);

  ngOnInit(): void {
    // If navigated directly (e.g. shared link), try to join
    const roomId = this.route.snapshot.paramMap.get('id');
    if (roomId && !this.gameService.currentRoom()) {
      this.gameService.joinRoom(roomId);
    }
  }
}
