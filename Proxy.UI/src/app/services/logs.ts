import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface LogEntry {
  id: string;
  timestamp: string;
  clientIp: string;
  method: string;
  path: string;
  queryString: string;
  clusterId: string;
  destinationAddress: string;
  statusCode: number;
  durationMs: number;
}

export interface LogResponse {
  data: LogEntry[];
  total: number;
}

@Injectable({
  providedIn: 'root'
})
export class LogsService {
  private http = inject(HttpClient);
  private apiUrl = '/api/logs';

  getLogs(limit: number = 100, offset: number = 0): Observable<LogResponse> {
    return this.http.get<LogResponse>(`${this.apiUrl}?limit=${limit}&offset=${offset}`);
  }

  clearLogs(): Observable<any> {
    return this.http.delete(`${this.apiUrl}/clear`);
  }
}
