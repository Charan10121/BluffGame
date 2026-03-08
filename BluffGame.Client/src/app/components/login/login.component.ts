import { Component, inject, signal, AfterViewInit, ElementRef, ViewChild, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent implements AfterViewInit {
  authService = inject(AuthService);
  private router = inject(Router);

  @ViewChild('googleBtn') googleBtnRef!: ElementRef;

  devName = '';
  isDevMode = !environment.production;

  constructor() {
    // Redirect when authenticated
    effect(() => {
      if (this.authService.isAuthenticated()) {
        this.router.navigate(['/lobby']);
      }
    });
  }

  ngAfterViewInit(): void {
    // Wait for Google Identity Services script to load
    this.waitForGoogle().then(() => {
      if (this.googleBtnRef?.nativeElement) {
        this.authService.initializeGoogleSignIn(this.googleBtnRef.nativeElement);
      }
    });
  }

  async devLogin(): Promise<void> {
    if (!this.devName.trim()) return;
    await this.authService.devLogin(this.devName.trim());
    if (this.authService.isAuthenticated()) {
      this.router.navigate(['/lobby']);
    }
  }

  private waitForGoogle(): Promise<void> {
    return new Promise((resolve) => {
      if ((window as any).google?.accounts?.id) {
        resolve();
        return;
      }
      // Poll until loaded (max 5s)
      let attempts = 0;
      const interval = setInterval(() => {
        attempts++;
        if ((window as any).google?.accounts?.id || attempts > 50) {
          clearInterval(interval);
          resolve();
        }
      }, 100);
    });
  }
}
