import { Routes } from '@angular/router';
import { authGuard } from './core/auth-guard';
import { changePasswordGuard } from './core/change-password-guard';
import { LoginComponent } from './pages/login/login';
import { DashboardComponent } from './pages/dashboard/dashboard';
import { RawEditorComponent } from './pages/raw-editor/raw-editor';
import { LogsComponent } from './pages/logs/logs';
import { HistoryComponent } from './pages/history/history';
import { ChangePasswordComponent } from './pages/change-password/change-password';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'change-password', component: ChangePasswordComponent, canActivate: [authGuard] },
  { path: 'dashboard', component: DashboardComponent, canActivate: [authGuard, changePasswordGuard] },
  { path: 'raw-editor', component: RawEditorComponent, canActivate: [authGuard, changePasswordGuard] },
  { path: 'logs', component: LogsComponent, canActivate: [authGuard, changePasswordGuard] },
  { path: 'history', component: HistoryComponent, canActivate: [authGuard, changePasswordGuard] },
  { path: '', redirectTo: '/dashboard', pathMatch: 'full' },
  { path: '**', redirectTo: '/dashboard' }
];

