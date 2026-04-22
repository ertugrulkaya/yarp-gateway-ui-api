// Proxy.UI/src/app/services/proxy-config.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, forkJoin } from 'rxjs';

// ── Interfaces ────────────────────────────────────────────────────────────────

export interface RouteHeaderConfig {
  name: string;
  values?: string[];
  mode?: string;
  isCaseSensitive?: boolean;
}

export interface RouteQueryParameterConfig {
  name: string;
  values?: string[];
  mode?: string;
  isCaseSensitive?: boolean;
}

export interface RouteMatchConfig {
  path?: string;
  methods?: string[];
  hosts?: string[];
  headers?: RouteHeaderConfig[];
  queryParameters?: RouteQueryParameterConfig[];
}

export interface RouteConfig {
  routeId: string;
  clusterId?: string;
  order?: number;
  match: RouteMatchConfig;
  transforms?: Record<string, string>[];
  authorizationPolicy?: string;
  corsPolicy?: string;
  rateLimiterPolicy?: string;
  timeoutPolicy?: string;
  outputCachePolicy?: string;
  maxRequestBodySize?: number;
  metadata?: Record<string, string>;
}

export interface DestinationConfig {
  address: string;
  health?: string;
  metadata?: Record<string, string>;
}

export interface SessionAffinityConfig {
  enabled?: boolean;
  policy?: string;
  failurePolicy?: string;
  affinityKeyName?: string;
  cookie?: {
    path?: string;
    domain?: string;
    httpOnly?: boolean;
    isEssential?: boolean;
    sameSite?: string;
    securePolicy?: string;
  };
}

export interface HealthCheckConfig {
  active?: {
    enabled?: boolean;
    interval?: string;
    timeout?: string;
    policy?: string;
    path?: string;
  };
  passive?: {
    enabled?: boolean;
    policy?: string;
    reactivationPeriod?: string;
  };
}

export interface HttpClientConfig {
  dangerousAcceptAnyServerCertificate?: boolean;
  maxConnectionsPerServer?: number;
  enableMultipleHttp1Connections?: boolean;
  enableMultipleHttp2Connections?: boolean;
  requestHeaderEncoding?: string;
  responseHeaderEncoding?: string;
}

export interface HttpRequestConfig {
  activityTimeout?: string;
  version?: string;
  versionPolicy?: string;
  allowResponseBuffering?: boolean;
}

export interface ClusterConfig {
  clusterId: string;
  loadBalancingPolicy?: string;
  destinations: Record<string, DestinationConfig>;
  sessionAffinity?: SessionAffinityConfig;
  healthCheck?: HealthCheckConfig;
  httpClient?: HttpClientConfig;
  httpRequest?: HttpRequestConfig;
  metadata?: Record<string, string>;
}

export interface ProxyConfigPayload {
  routes: RouteConfig[];
  clusters: ClusterConfig[];
}

// ── Service ───────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class ProxyConfigService {
  private http = inject(HttpClient);
  private base = '/api/proxyconfig';

  // Bulk
  getRawConfig(): Observable<ProxyConfigPayload> {
    return this.http.get<ProxyConfigPayload>(`${this.base}/raw`);
  }
  updateRawConfig(payload: ProxyConfigPayload): Observable<any> {
    return this.http.post(`${this.base}/raw`, payload);
  }
  seedDefaultConfig(): Observable<any> {
    return this.http.post(`${this.base}/seed`, {});
  }

  // Routes
  getRoutes(): Observable<RouteConfig[]> {
    return this.http.get<RouteConfig[]>(`${this.base}/routes`);
  }
  addRoute(route: RouteConfig): Observable<any> {
    return this.http.post(`${this.base}/routes`, route);
  }
  updateRoute(routeId: string, route: RouteConfig): Observable<any> {
    return this.http.put(`${this.base}/routes/${routeId}`, route);
  }
  deleteRoute(routeId: string): Observable<any> {
    return this.http.delete(`${this.base}/routes/${routeId}`);
  }

  // Clusters
  getClusters(): Observable<ClusterConfig[]> {
    return this.http.get<ClusterConfig[]>(`${this.base}/clusters`);
  }
  addCluster(cluster: ClusterConfig): Observable<any> {
    return this.http.post(`${this.base}/clusters`, cluster);
  }
  updateCluster(clusterId: string, cluster: ClusterConfig): Observable<any> {
    return this.http.put(`${this.base}/clusters/${clusterId}`, cluster);
  }
  deleteCluster(clusterId: string): Observable<any> {
    return this.http.delete(`${this.base}/clusters/${clusterId}`);
  }

  // Load both at once (for dashboard init)
  loadAll(): Observable<{ routes: RouteConfig[]; clusters: ClusterConfig[] }> {
    return forkJoin({
      routes: this.getRoutes(),
      clusters: this.getClusters(),
    });
  }

  backup(): Observable<Blob> {
    return this.http.get(`${this.base}/backup`, { responseType: 'blob' });
  }

  restore(payload: { routes: RouteConfig[]; clusters: ClusterConfig[] }): Observable<any> {
    return this.http.post(`${this.base}/restore`, payload);
  }

  getHistory(limit = 100, offset = 0): Observable<{ data: ConfigHistoryEntry[]; total: number }> {
    return this.http.get<{ data: ConfigHistoryEntry[]; total: number }>(
      `${this.base}/history?limit=${limit}&offset=${offset}`
    );
  }

  getSummary(): Observable<DashboardSummary> {
    return this.http.get<DashboardSummary>(`${this.base}/summary`);
  }
}

export interface DashboardSummary {
  routes: number;
  clusters: number;
  requestsTotal: number;
  errorsLast24h: number;
}

export interface ConfigHistoryEntry {
  id: string;
  entityType: string;
  entityId: string;
  action: string;
  changedBy: string;
  changedAt: string;
  oldValueJson: string | null;
  newValueJson: string | null;
}
