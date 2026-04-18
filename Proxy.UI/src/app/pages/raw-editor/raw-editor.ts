// Proxy.UI/src/app/pages/raw-editor/raw-editor.ts
import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBarModule, MatSnackBar } from '@angular/material/snack-bar';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ProxyConfigService } from '../../services/proxy-config';

@Component({
  selector: 'app-raw-editor',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatCardModule, MatButtonModule,
    MatInputModule, MatSnackBarModule, MatIconModule, MatTooltipModule,
  ],
  templateUrl: './raw-editor.html',
  styleUrls: ['./raw-editor.css'],
})
export class RawEditorComponent implements OnInit {
  private proxyService = inject(ProxyConfigService);
  private snackBar = inject(MatSnackBar);
  private cdr = inject(ChangeDetectorRef);

  rawJson = '';
  importJson = '';
  showImport = false;

  ngOnInit() {
    this.proxyService.getRawConfig().subscribe({
      next: (config) => {
        this.rawJson = JSON.stringify(config, null, 2);
        this.cdr.markForCheck();
      },
    });
  }

  saveConfig() {
    try {
      const parsed = JSON.parse(this.rawJson);
      if (!Array.isArray(parsed.routes) || !Array.isArray(parsed.clusters)) {
        throw new Error('Config must contain "routes" and "clusters" arrays.');
      }
      this.proxyService.updateRawConfig(parsed).subscribe({
        next: () => this.snackBar.open('Configuration saved & applied to YARP!', 'Close', { duration: 4000 }),
        error: () => this.snackBar.open('Error saving configuration.', 'Close', { duration: 4000 }),
      });
    } catch (e: any) {
      this.snackBar.open('Invalid JSON: ' + e.message, 'Close', { duration: 5000 });
    }
  }

  // ── YARP appsettings import ────────────────────────────────────────────────

  toggleImport() {
    this.showImport = !this.showImport;
    this.importJson = '';
  }

  convertAndMerge() {
    try {
      const imported = JSON.parse(this.importJson);
      const { routes: newRoutes, clusters: newClusters } = this.parseYarpFormat(imported);

      const current = JSON.parse(this.rawJson);
      const existingRouteIds = new Set((current.routes as any[]).map((r: any) => r.routeId));
      const existingClusterIds = new Set((current.clusters as any[]).map((c: any) => c.clusterId));

      const addedRoutes = newRoutes.filter(r => !existingRouteIds.has(r.routeId));
      const addedClusters = newClusters.filter(c => !existingClusterIds.has(c.clusterId));
      const skippedRoutes = newRoutes.length - addedRoutes.length;
      const skippedClusters = newClusters.length - addedClusters.length;

      current.routes.push(...addedRoutes);
      current.clusters.push(...addedClusters);

      this.rawJson = JSON.stringify(current, null, 2);
      this.showImport = false;
      this.importJson = '';
      this.cdr.markForCheck();

      const msg = `Merged: +${addedRoutes.length} routes, +${addedClusters.length} clusters`
        + (skippedRoutes || skippedClusters ? ` (skipped ${skippedRoutes + skippedClusters} duplicates)` : '')
        + '. Click "Save & Apply" to apply.';
      this.snackBar.open(msg, 'Close', { duration: 6000 });
    } catch (e: any) {
      this.snackBar.open('Parse error: ' + e.message, 'Close', { duration: 5000 });
    }
  }

  // ── Format detection & conversion ─────────────────────────────────────────

  private parseYarpFormat(obj: any): { routes: any[]; clusters: any[] } {
    // Detect format:
    // 1. { ReverseProxy: { Routes: {}, Clusters: {} } }  — full appsettings
    // 2. { Routes: {}, Clusters: {} }                    — ReverseProxy section
    // 3. { routeId: { ClusterId, Match, ... } }           — routes-only dict
    // 4. { clusters: {}, routes: {} }  or array format    — already our format

    let routesDict: Record<string, any> = {};
    let clustersDict: Record<string, any> = {};

    if (obj.ReverseProxy) {
      // full appsettings
      routesDict = obj.ReverseProxy.Routes ?? obj.ReverseProxy.routes ?? {};
      clustersDict = obj.ReverseProxy.Clusters ?? obj.ReverseProxy.clusters ?? {};
    } else if (obj.Routes || obj.Clusters) {
      // ReverseProxy section
      routesDict = obj.Routes ?? obj.routes ?? {};
      clustersDict = obj.Clusters ?? obj.clusters ?? {};
    } else {
      // Guess: if all values have a "ClusterId" or "Match" key → routes dict
      const values = Object.values(obj);
      const looksLikeRoutes = values.every(
        (v: any) => v && typeof v === 'object' && (v.ClusterId || v.clusterId || v.Match || v.match)
      );
      if (looksLikeRoutes) {
        routesDict = obj;
      } else {
        throw new Error(
          'Unrecognized format. Expected YARP appsettings format: ' +
          '{ "Routes": { "routeId": {...} }, "Clusters": { "clusterId": {...} } }'
        );
      }
    }

    const routes = Object.entries(routesDict).map(([id, r]) => this.convertRoute(id, r));
    const clusters = Object.entries(clustersDict).map(([id, c]) => this.convertCluster(id, c));
    return { routes, clusters };
  }

  private convertRoute(routeId: string, r: any): any {
    const match = r.Match ?? r.match ?? {};
    const headers = (match.Headers ?? match.headers ?? []).map((h: any) => ({
      name: h.Name ?? h.name ?? '',
      values: h.Values ?? h.values,
      mode: h.Mode ?? h.mode,
      isCaseSensitive: h.IsCaseSensitive ?? h.isCaseSensitive ?? false,
    }));
    const queryParameters = (match.QueryParameters ?? match.queryParameters ?? []).map((q: any) => ({
      name: q.Name ?? q.name ?? '',
      values: q.Values ?? q.values,
      mode: q.Mode ?? q.mode,
      isCaseSensitive: q.IsCaseSensitive ?? q.isCaseSensitive ?? false,
    }));

    // Transforms: keep entry keys as-is (YARP reads them case-sensitively)
    const transforms = (r.Transforms ?? r.transforms ?? []).map((t: any) => {
      const out: Record<string, string> = {};
      for (const k of Object.keys(t)) out[k] = t[k];
      return out;
    });

    const route: any = {
      routeId,
      clusterId: r.ClusterId ?? r.clusterId ?? null,
      order: r.Order ?? r.order ?? null,
      match: {
        path: match.Path ?? match.path,
        methods: match.Methods ?? match.methods,
        hosts: match.Hosts ?? match.hosts,
        headers: headers.length ? headers : undefined,
        queryParameters: queryParameters.length ? queryParameters : undefined,
      },
      transforms: transforms.length ? transforms : undefined,
      authorizationPolicy: r.AuthorizationPolicy ?? r.authorizationPolicy,
      corsPolicy: r.CorsPolicy ?? r.corsPolicy,
      rateLimiterPolicy: r.RateLimiterPolicy ?? r.rateLimiterPolicy,
      timeoutPolicy: r.TimeoutPolicy ?? r.timeoutPolicy,
      outputCachePolicy: r.OutputCachePolicy ?? r.outputCachePolicy,
      maxRequestBodySize: r.MaxRequestBodySize ?? r.maxRequestBodySize,
      metadata: r.Metadata ?? r.metadata,
    };

    // Strip undefined/null optional fields
    return JSON.parse(JSON.stringify(route, (_, v) => v === undefined ? undefined : v));
  }

  private convertCluster(clusterId: string, c: any): any {
    const rawDests = c.Destinations ?? c.destinations ?? {};
    const destinations: Record<string, any> = {};
    for (const [key, d] of Object.entries(rawDests) as [string, any][]) {
      destinations[key] = {
        address: d.Address ?? d.address ?? '',
        health: d.Health ?? d.health,
        metadata: d.Metadata ?? d.metadata,
      };
    }

    const sa = c.SessionAffinity ?? c.sessionAffinity;
    const hc = c.HealthCheck ?? c.healthCheck;
    const httpClient = c.HttpClient ?? c.httpClient;
    const httpRequest = c.HttpRequest ?? c.httpRequest;

    const cluster: any = {
      clusterId,
      loadBalancingPolicy: c.LoadBalancingPolicy ?? c.loadBalancingPolicy,
      destinations,
      sessionAffinity: sa ? {
        enabled: sa.Enabled ?? sa.enabled,
        policy: sa.Policy ?? sa.policy,
        failurePolicy: sa.FailurePolicy ?? sa.failurePolicy,
        affinityKeyName: sa.AffinityKeyName ?? sa.affinityKeyName,
        cookie: (sa.Cookie ?? sa.cookie) ? {
          path: (sa.Cookie ?? sa.cookie)?.Path ?? (sa.Cookie ?? sa.cookie)?.path,
          domain: (sa.Cookie ?? sa.cookie)?.Domain ?? (sa.Cookie ?? sa.cookie)?.domain,
          httpOnly: (sa.Cookie ?? sa.cookie)?.HttpOnly ?? (sa.Cookie ?? sa.cookie)?.httpOnly,
          isEssential: (sa.Cookie ?? sa.cookie)?.IsEssential ?? (sa.Cookie ?? sa.cookie)?.isEssential,
          sameSite: (sa.Cookie ?? sa.cookie)?.SameSite ?? (sa.Cookie ?? sa.cookie)?.sameSite,
          securePolicy: (sa.Cookie ?? sa.cookie)?.SecurePolicy ?? (sa.Cookie ?? sa.cookie)?.securePolicy,
        } : undefined,
      } : undefined,
      healthCheck: hc ? {
        active: (hc.Active ?? hc.active) ? {
          enabled: (hc.Active ?? hc.active)?.Enabled ?? (hc.Active ?? hc.active)?.enabled,
          interval: (hc.Active ?? hc.active)?.Interval ?? (hc.Active ?? hc.active)?.interval,
          timeout: (hc.Active ?? hc.active)?.Timeout ?? (hc.Active ?? hc.active)?.timeout,
          policy: (hc.Active ?? hc.active)?.Policy ?? (hc.Active ?? hc.active)?.policy,
          path: (hc.Active ?? hc.active)?.Path ?? (hc.Active ?? hc.active)?.path,
        } : undefined,
        passive: (hc.Passive ?? hc.passive) ? {
          enabled: (hc.Passive ?? hc.passive)?.Enabled ?? (hc.Passive ?? hc.passive)?.enabled,
          policy: (hc.Passive ?? hc.passive)?.Policy ?? (hc.Passive ?? hc.passive)?.policy,
          reactivationPeriod: (hc.Passive ?? hc.passive)?.ReactivationPeriod ?? (hc.Passive ?? hc.passive)?.reactivationPeriod,
        } : undefined,
      } : undefined,
      httpClient: httpClient ? {
        dangerousAcceptAnyServerCertificate: httpClient.DangerousAcceptAnyServerCertificate ?? httpClient.dangerousAcceptAnyServerCertificate,
        maxConnectionsPerServer: httpClient.MaxConnectionsPerServer ?? httpClient.maxConnectionsPerServer,
        enableMultipleHttp2Connections: httpClient.EnableMultipleHttp2Connections ?? httpClient.enableMultipleHttp2Connections,
        requestHeaderEncoding: httpClient.RequestHeaderEncoding ?? httpClient.requestHeaderEncoding,
        responseHeaderEncoding: httpClient.ResponseHeaderEncoding ?? httpClient.responseHeaderEncoding,
      } : undefined,
      httpRequest: httpRequest ? {
        activityTimeout: httpRequest.ActivityTimeout ?? httpRequest.activityTimeout,
        version: httpRequest.Version ?? httpRequest.version,
        versionPolicy: httpRequest.VersionPolicy ?? httpRequest.versionPolicy,
        allowResponseBuffering: httpRequest.AllowResponseBuffering ?? httpRequest.allowResponseBuffering,
      } : undefined,
      metadata: c.Metadata ?? c.metadata,
    };

    return JSON.parse(JSON.stringify(cluster, (_, v) => v === undefined ? undefined : v));
  }
}
