import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatTooltipModule } from '@angular/material/tooltip';

import { AuthService } from '../core/auth/auth.service';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, MatToolbarModule, MatButtonModule, MatIconModule, MatTooltipModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-toolbar class="toolbar">
      <a routerLink="/tasks" class="brand">
        <mat-icon aria-hidden="true">task_alt</mat-icon>
        <span>Task Manager</span>
      </a>
      <span class="spacer"></span>
      @if (auth.currentUser(); as user) {
        <span class="user" data-testid="current-user">
          <mat-icon aria-hidden="true">account_circle</mat-icon>
          <span class="username">{{ user.username }}</span>
        </span>
      }
      <button mat-icon-button matTooltip="Sign out" aria-label="Sign out" (click)="auth.logout()">
        <mat-icon>logout</mat-icon>
      </button>
    </mat-toolbar>

    <main>
      <router-outlet />
    </main>
  `,
  styles: `
    .toolbar {
      position: sticky;
      top: 0;
      z-index: 10;
      background: var(--mat-sys-primary);
      color: var(--mat-sys-on-primary);
      --mat-icon-button-icon-color: var(--mat-sys-on-primary);
    }
    .brand {
      display: flex;
      align-items: center;
      gap: 8px;
      color: inherit;
      text-decoration: none;
      font-weight: 500;
    }
    .spacer {
      flex: 1;
    }
    .user {
      display: flex;
      align-items: center;
      gap: 6px;
      font: var(--mat-sys-body-large);
      margin-right: 4px;
    }
    @media (max-width: 480px) {
      .username {
        display: none;
      }
    }
  `,
})
export class Shell {
  protected readonly auth = inject(AuthService);
}
