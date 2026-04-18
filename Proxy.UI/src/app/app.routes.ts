import { Routes } from '@angular/router';
import { authGuard } from './core/auth-guard';
import { LoginComponent } from './pages/login/login';
import { DashboardComponent } from './pages/dashboard/dashboard';
import { RawEditorComponent } from './pages/raw-editor/raw-editor';
import { LogsComponent } from './pages/logs/logs';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'dashboard', component: DashboardComponent, canActivate: [authGuard] },
  { path: 'raw-editor', component: RawEditorComponent, canActivate: [authGuard] },
  { path: 'logs', component: LogsComponent, canActivate: [authGuard] },
  { path: '', redirectTo: '/dashboard', pathMatch: 'full' },
  { path: '**', redirectTo: '/dashboard' }
];

