import { Injectable, signal } from '@angular/core';
import { environment } from '../../environments/environment';

const STORAGE_TOKEN = 'bluff_auth_token';
const STORAGE_AUTH_USER = 'bluff_auth_user';

export interface AuthUser {
  playerId: string;
  name: string;
  email: string;
  picture: string;
}

interface AuthResponse {
  token: string;
  playerId: string;
  name: string;
  email: string;
  picture: string;
}

/**
 * Handles Google OAuth 2.0 / OIDC sign-in and JWT token management.
 *
 * Flow:
 * 1. Load Google Identity Services script
 * 2. User clicks "Sign in with Google"
 * 3. Google returns ID token
 * 4. Send to /api/auth/google → receive app JWT
 * 5. Store JWT for SignalR auth
 */
@Injectable({ providedIn: 'root' })
export class AuthService {

  user = signal<AuthUser | null>(null);
  isAuthenticated = signal(false);
  isLoading = signal(false);

  private token: string | null = null;

  constructor() {
    this.restoreSession();
  }

  /** Get the current JWT for SignalR connection. */
  getToken(): string | null {
    return this.token;
  }

  /** Initialize Google Identity Services button in a container element. */
  initializeGoogleSignIn(buttonElement: HTMLElement): void {
    const google = (window as any).google;
    if (!google?.accounts?.id) {
      console.warn('Google Identity Services not loaded yet');
      return;
    }

    google.accounts.id.initialize({
      client_id: environment.googleClientId,
      callback: (response: any) => this.handleGoogleCallback(response),
      auto_select: false,
      cancel_on_tap_outside: true
    });

    google.accounts.id.renderButton(buttonElement, {
      theme: 'outline',
      size: 'large',
      text: 'signin_with',
      shape: 'rectangular',
      width: 300
    });
  }

  /** Dev-only: sign in without Google. */
  async devLogin(name: string): Promise<void> {
    this.isLoading.set(true);
    try {
      const response = await fetch('/api/auth/dev', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name })
      });

      if (!response.ok) throw new Error('Dev login failed');

      const data: AuthResponse = await response.json();
      this.setSession(data);
    } finally {
      this.isLoading.set(false);
    }
  }

  /** Update the display name locally. */
  updateUserName(newName: string): void {
    const current = this.user();
    if (current) {
      const updated = { ...current, name: newName };
      this.user.set(updated);
      localStorage.setItem(STORAGE_AUTH_USER, JSON.stringify(updated));
      // Also sync with game service storage
      localStorage.setItem('bluff_player_name', newName);
    }
  }

  /** Sign out and clear stored session. */
  signOut(): void {
    this.token = null;
    this.user.set(null);
    this.isAuthenticated.set(false);
    localStorage.removeItem(STORAGE_TOKEN);
    localStorage.removeItem(STORAGE_AUTH_USER);

    // Revoke Google session if available
    const google = (window as any).google;
    if (google?.accounts?.id) {
      google.accounts.id.disableAutoSelect();
    }
  }

  // ── Private ───────────────────────────────────────────────────────

  private async handleGoogleCallback(response: any): Promise<void> {
    if (!response.credential) return;

    this.isLoading.set(true);
    try {
      const res = await fetch('/api/auth/google', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ idToken: response.credential })
      });

      if (!res.ok) throw new Error('Google auth failed');

      const data: AuthResponse = await res.json();
      this.setSession(data);
    } catch (err) {
      console.error('Google sign-in error:', err);
    } finally {
      this.isLoading.set(false);
    }
  }

  private setSession(data: AuthResponse): void {
    this.token = data.token;
    const user: AuthUser = {
      playerId: data.playerId,
      name: data.name,
      email: data.email,
      picture: data.picture
    };
    this.user.set(user);
    this.isAuthenticated.set(true);

    localStorage.setItem(STORAGE_TOKEN, data.token);
    localStorage.setItem(STORAGE_AUTH_USER, JSON.stringify(user));
  }

  private restoreSession(): void {
    const token = localStorage.getItem(STORAGE_TOKEN);
    const userJson = localStorage.getItem(STORAGE_AUTH_USER);

    if (token && userJson) {
      try {
        // Check if token is expired
        const payload = JSON.parse(atob(token.split('.')[1]));
        if (payload.exp * 1000 > Date.now()) {
          this.token = token;
          this.user.set(JSON.parse(userJson));
          this.isAuthenticated.set(true);
        } else {
          this.signOut();
        }
      } catch {
        this.signOut();
      }
    }
  }
}
