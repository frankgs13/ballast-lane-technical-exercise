import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { AuthResponse } from './auth.models';
import { authInterceptor } from './auth.interceptor';
import { safeReturnUrl } from './auth.guards';
import { AuthService } from './auth.service';

const STORAGE_KEY = 'taskmanager.session';

function authResponse(expiresInMs = 60 * 60 * 1000): AuthResponse {
  return {
    accessToken: 'jwt-token',
    expiresAtUtc: new Date(Date.now() + expiresInMs).toISOString(),
    user: { id: '1', username: 'demo', email: 'demo@example.com', createdAtUtc: new Date().toISOString() },
  };
}

describe('AuthService', () => {
  let http: HttpTestingController;

  function setup(): AuthService {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    return TestBed.inject(AuthService);
  }

  beforeEach(() => localStorage.clear());
  afterEach(() => http.verify());

  it('starts a session on login and persists it', () => {
    const auth = setup();

    auth.login({ username: 'demo', password: 'Demo123!' }).subscribe();
    http.expectOne('/api/auth/login').flush(authResponse());

    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.currentUser()?.username).toBe('demo');
    expect(localStorage.getItem(STORAGE_KEY)).toContain('jwt-token');
  });

  it('restores a valid stored session', () => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(authResponse()));

    expect(setup().isAuthenticated()).toBe(true);
  });

  it('discards an expired stored session', () => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(authResponse(-1000)));

    expect(setup().isAuthenticated()).toBe(false);
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
  });

  it('survives corrupted storage', () => {
    localStorage.setItem(STORAGE_KEY, '{not json');

    expect(setup().isAuthenticated()).toBe(false);
  });

  it('logout clears the session and redirects to login', () => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(authResponse()));
    const auth = setup();

    auth.logout();

    expect(auth.isAuthenticated()).toBe(false);
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
    expect(TestBed.inject(Router).navigate).toHaveBeenCalledWith(['/login'], { queryParams: undefined });
  });

  describe('authInterceptor', () => {
    it('adds the bearer token to API calls', () => {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(authResponse()));
      setup();

      TestBed.inject(HttpClient).get('/api/tasks').subscribe();

      expect(http.expectOne('/api/tasks').request.headers.get('Authorization')).toBe('Bearer jwt-token');
    });

    it('never sends the token to other origins', () => {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(authResponse()));
      setup();

      TestBed.inject(HttpClient).get('https://example.com/data').subscribe();

      expect(http.expectOne('https://example.com/data').request.headers.has('Authorization')).toBe(false);
    });

    it('logs out when the API rejects the token', () => {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(authResponse()));
      const auth = setup();

      TestBed.inject(HttpClient).get('/api/tasks').subscribe({ error: () => undefined });
      http.expectOne('/api/tasks').flush(null, { status: 401, statusText: 'Unauthorized' });

      expect(auth.isAuthenticated()).toBe(false);
    });

    it('does not log out on a failed login attempt', () => {
      const auth = setup();

      auth.login({ username: 'demo', password: 'wrong' }).subscribe({ error: () => undefined });
      http.expectOne('/api/auth/login').flush(null, { status: 401, statusText: 'Unauthorized' });

      expect(TestBed.inject(Router).navigate).not.toHaveBeenCalled();
    });
  });

  describe('safeReturnUrl', () => {
    it.each([
      ['/tasks?x=1', '/tasks?x=1'],
      ['//evil.com', '/tasks'],
      ['https://evil.com', '/tasks'],
      [undefined, '/tasks'],
    ])('maps %s to %s', (input, expected) => {
      expect(safeReturnUrl(input)).toBe(expected);
    });
  });
});
