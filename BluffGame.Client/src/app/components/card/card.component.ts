import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CardDto } from '../../models';

@Component({
  selector: 'app-card',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './card.component.html',
  styleUrl: './card.component.scss'
})
export class CardComponent {
  @Input({ required: true }) card!: CardDto;
  @Input() selected = false;
  @Input() selectable = false;
  @Input() faceDown = false;

  @Output() cardClick = new EventEmitter<void>();

  onClick(): void {
    if (this.selectable) {
      this.cardClick.emit();
    }
  }
}
