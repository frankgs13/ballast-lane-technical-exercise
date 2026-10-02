import { HttpClient } from '@angular/common/http';
import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, map, tap } from 'rxjs';

import { AuthResponse, LoginRequest, RegisterRequest, UserProfile } from './auth.models';

const STORAGE_KEY = 'taskmanager.session';

// setTimeout overflows above ~24.8 days.
const MAX_TIMER_MS = 2_147_483_647;

/**
 * Owns the session (token + profile) as a signal so the UI reacts to login/logout instantly.
 *
 * Trade-off: the token lives in localStorage to keep the API stateless and the demo simple.
 * An httpOnly cookie would be safer against XSS; Angular's default template escaping and the
 * absence of innerHTML usage mitigate that risk here.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly session = signal<AuthResponse | null>(readStoredSession());
  private expiryTimer: ReturnType<typeof setTimeout> | undefined;

  readonly currentUser = computed<UserProfile | null>(() => this.session()?.user ?? null);
  readonly isAuthenticated = computed(() => this.session() !== null);

  constructor() {
    this.scheduleExpiry(this.session());
    inject(DestroyRef).onDestroy(() => clearTimeout(this.expiryTimer));
  }

  get accessToken(): string | null {
    const session = this.session();
    return session && !isExpired(session) ? session.accessToken : null;
  }

  login(request: LoginRequest): Observable<UserProfile> {
    return this.http.post<AuthResponse>('/api/auth/login', request).pipe(
      tap((response) => this.startSession(response)),
      map((response) => response.user),
    );
  }

  register(request: RegisterRequest): Observable<UserProfile> {
    return this.http.post<AuthResponse>('/api/auth/register', request).pipe(
      tap((response) => this.startSession(response)),
      map((response) => response.user),
    );
  }

  logout(options: { redirect?: boolean; reason?: 'expired' } = {}): void {
    clearTimeout(this.expiryTimer);
    this.session.set(null);
    localStorage.removeItem(STORAGE_KEY);

    if (options.redirect ?? true) {
      void this.router.navigate(['/login'], {
        queryParams: options.reason ? { reason: options.reason } : undefined,
      });
    }
  }

  private startSession(response: AuthResponse): void {
    this.session.set(response);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(response));
    this.scheduleExpiry(response);
  }

  private scheduleExpiry(session: AuthResponse | null): void {
    clearTimeout(this.expiryTimer);
    if (!session) {
      return;
    }

    const msUntilExpiry = new Date(session.expiresAtUtc).getTime() - Date.now();
    this.expiryTimer = setTimeout(
      () => this.logout({ reason: 'expired' }),
      Math.min(Math.max(msUntilExpiry, 0), MAX_TIMER_MS),
    );
  }
}

function isExpired(session: AuthResponse): boolean {
  return new Date(session.expiresAtUtc).getTime() <= Date.now();
}

function readStoredSession(): AuthResponse | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return null;
    }

    const session = JSON.parse(raw) as AuthResponse;
    if (!session?.accessToken || !session.user || isExpired(session)) {
      localStorage.removeItem(STORAGE_KEY);
      return null;
    }

    return session;
  } catch {
    // Corrupted storage must never brick the app.
    localStorage.removeItem(STORAGE_KEY);
    return null;
  }
}
