import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs/operators';

export interface LoginResponse {
  token: string;
  mustChangePassword: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);
  
  private apiUrl = '/api/auth';

  login(credentials: any) {
    return this.http.post<LoginResponse>(`${this.apiUrl}/login`, credentials).pipe(
      tap(res => {
        if (res && res.token) {
          localStorage.setItem('access_token', res.token);
          localStorage.setItem('must_change_password', res.mustChangePassword ? 'true' : 'false');
        }
      })
    );
  }

  logout() {
    localStorage.removeItem('access_token');
    localStorage.removeItem('must_change_password');
    this.router.navigate(['/login']);
  }

  isAuthenticated(): boolean {
    return !!localStorage.getItem('access_token');
  }

  mustChangePassword(): boolean {
    return localStorage.getItem('must_change_password') === 'true';
  }

  changePassword(data: any) {
    return this.http.post<{ message: string; token: string }>(`${this.apiUrl}/change-password`, data).pipe(
      tap(res => {
        if (res?.token) {
          localStorage.setItem('access_token', res.token);
          localStorage.setItem('must_change_password', 'false');
        }
      })
    );
  }
}
