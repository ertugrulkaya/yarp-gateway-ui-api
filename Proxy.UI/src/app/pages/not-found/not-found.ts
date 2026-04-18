// Proxy.UI/src/app/pages/not-found/not-found.ts
import { Component, ChangeDetectionStrategy } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-not-found',
  standalone: true,
  imports: [RouterLink, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="not-found-container">
      <mat-icon class="not-found-icon">explore_off</mat-icon>
      <h1 class="not-found-code">404</h1>
      <p class="not-found-message">Page not found</p>
      <p class="not-found-sub">The page you're looking for doesn't exist or has been moved.</p>
      <a mat-raised-button color="primary" routerLink="/dashboard">
        <mat-icon>dashboard</mat-icon> Go to Dashboard
      </a>
    </div>
  `,
  styles: [`
    .not-found-container {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      min-height: 60vh;
      gap: 12px;
      text-align: center;
      padding: 32px;
    }
    .not-found-icon {
      font-size: 72px;
      width: 72px;
      height: 72px;
      color: #bdbdbd;
    }
    .not-found-code {
      font-size: 6rem;
      font-weight: 700;
      margin: 0;
      line-height: 1;
      color: #9e9e9e;
    }
    .not-found-message {
      font-size: 1.5rem;
      font-weight: 500;
      margin: 0;
      color: #424242;
    }
    .not-found-sub {
      font-size: 1rem;
      color: #757575;
      margin: 0 0 16px;
    }
  `],
})
export class NotFoundComponent {}
