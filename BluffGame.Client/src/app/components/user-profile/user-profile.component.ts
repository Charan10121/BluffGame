import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-user-profile',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './user-profile.component.html',
  styleUrl: './user-profile.component.scss'
})
export class UserProfileComponent {
  authService = inject(AuthService);
  private router = inject(Router);

  isOpen = signal(false);
  isEditing = signal(false);
  editName = '';

  toggle(): void {
    this.isOpen.update(v => !v);
    this.isEditing.set(false);
  }

  close(): void {
    this.isOpen.set(false);
    this.isEditing.set(false);
  }

  startEditing(): void {
    this.editName = this.authService.user()?.name ?? '';
    this.isEditing.set(true);
  }

  saveName(): void {
    const trimmed = this.editName.trim();
    if (!trimmed) return;

    const current = this.authService.user();
    if (current) {
      this.authService.updateUserName(trimmed);
    }
    this.isEditing.set(false);
  }

  cancelEdit(): void {
    this.isEditing.set(false);
  }

  logout(): void {
    this.close();
    this.authService.signOut();
    this.router.navigate(['/login']);
  }

  /** Get initials for avatar fallback */
  getInitials(): string {
    const name = this.authService.user()?.name ?? '';
    return name.split(' ').map(w => w[0]).join('').toUpperCase().slice(0, 2);
  }
}
