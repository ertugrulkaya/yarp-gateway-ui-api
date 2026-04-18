import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface LogEntry {
  id: string;
  timestamp: string;
  clientIp: string;
  method: string;
  path: string;
  clusterId: string;
  destinationAddress: string;
  statusCode: number;
  durationMs: number;
}

export interface LogResponse {
  data: LogEntry[];
  total: number;
}

export interface LogFilters {
  clusterId?: string;
  statusCode?: number;
  clientIp?: string;
  method?: string;
  sortBy?: string;
  sortDir?: string;
}

@Injectable({
  providedIn: 'root'
})
export class LogsService {
  private http = inject(HttpClient);
  private apiUrl = '/api/logs';

  getLogs(
    limit: number = 100,
    offset: number = 0,
    filters: LogFilters = {}
  ): Observable<LogResponse> {
    const params: Record<string, string> = { limit: String(limit), offset: String(offset) };
    if (filters.clusterId)  params['clusterId']  = filters.clusterId;
    if (filters.statusCode) params['statusCode'] = String(filters.statusCode);
    if (filters.clientIp)   params['clientIp']   = filters.clientIp;
    if (filters.method)     params['method']     = filters.method;
    if (filters.sortBy)     params['sortBy']     = filters.sortBy;
    if (filters.sortDir)    params['sortDir']    = filters.sortDir;
    return this.http.get<LogResponse>(this.apiUrl, { params });
  }

  clearLogs(): Observable<any> {
    return this.http.delete(`${this.apiUrl}/clear`);
  }
}
