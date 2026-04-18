// Proxy.UI/src/app/pages/dashboard/dashboard.ts
import {
  Component, OnInit, OnDestroy, inject,
  signal, computed, ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatSnackBarModule, MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, NavigationEnd } from '@angular/router';
import { Subscription } from 'rxjs';
import { filter } from 'rxjs/operators';
import { ProxyConfigService, RouteConfig, ClusterConfig, DashboardSummary } from '../../services/proxy-config';
import { RouteDialogComponent } from './dialogs/route-dialog';
import { ClusterDialogComponent } from './dialogs/cluster-dialog';
import { RawEditDialogComponent } from './dialogs/raw-edit-dialog';
import { ServiceWizardDialogComponent } from './dialogs/service-wizard-dialog';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    MatTableModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    MatSnackBarModule,
    MatTooltipModule,
  ],
  templateUrl: './dashboard.html',
  styleUrls: ['./dashboard.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardComponent implements OnInit, OnDestroy {
  private proxyService = inject(ProxyConfigService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private router = inject(Router);

  private routerSub?: Subscription;

  // ── State signals ───────────────────────────────────────────────────────────
  readonly routes = signal<RouteConfig[]>([]);
  readonly clusters = signal<ClusterConfig[]>([]);
  readonly isSaving = signal(false);
  readonly isLoading = signal(false);
  readonly skeletonRows = Array(4).fill(0);
  readonly summary = signal<DashboardSummary | null>(null);

  // Derived: cluster ID list for route dialog dropdown
  readonly clusterRefs = computed(() => this.clusters().map(c => ({ clusterId: c.clusterId })));

  readonly routesColumns = ['RouteId', 'ClusterId', 'Match', 'Actions'];
  readonly clustersColumns = ['ClusterId', 'Destinations', 'Actions'];

  ngOnInit() {
    this.loadConfig();
    this.loadSummary();
    this.routerSub = this.router.events.pipe(
      filter(e => e instanceof NavigationEnd && e.urlAfterRedirects.includes('/dashboard'))
    ).subscribe(() => { this.loadConfig(); this.loadSummary(); });
  }

  ngOnDestroy() { this.routerSub?.unsubscribe(); }

  goToLogs() { this.router.navigate(['/logs']); }

  loadSummary() {
    this.proxyService.getSummary().subscribe({
      next: (s) => this.summary.set(s),
      error: () => { /* summary is non-critical, silently ignore */ },
    });
  }

  loadConfig() {
    this.isLoading.set(true);
    this.proxyService.loadAll().subscribe({
      next: ({ routes, clusters }) => {
        this.routes.set(routes);
        this.clusters.set(clusters);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.snackBar.open('Error loading configuration.', 'Close', { duration: 3000 });
      },
    });
  }

  // ── Service Wizard ─────────────────────────────────────────────────────────

  openServiceWizard() {
    if (this.isSaving()) return;
    const ref = this.dialog.open(ServiceWizardDialogComponent, {
      width: '600px',
      data: { existingClusters: this.clusters() },
    });
    ref.afterClosed().subscribe((result: { routes: RouteConfig[]; cluster: ClusterConfig } | undefined) => {
      if (!result) return;
      const payload = {
        routes: [...this.routes(), ...result.routes],
        clusters: [...this.clusters(), result.cluster],
      };
      this.isSaving.set(true);
      this.proxyService.updateRawConfig(payload).subscribe({
        next: () => { this.snackBar.open('Service added!', 'Close', { duration: 3000 }); this.loadConfig(); },
        error: () => this.snackBar.open('Error saving configuration.', 'Close', { duration: 3000 }),
      }).add(() => this.isSaving.set(false));
    });
  }

  // ── Route CRUD ─────────────────────────────────────────────────────────────

  addRoute() {
    if (this.isSaving()) return;
    const ref = this.dialog.open(RouteDialogComponent, {
      width: '750px',
      data: { clusters: this.clusterRefs(), existingRoutes: this.routes() },
    });
    ref.afterClosed().subscribe((result: RouteConfig | undefined) => {
      if (!result) return;
      this.isSaving.set(true);
      this.proxyService.addRoute(result).subscribe({
        next: () => { this.snackBar.open('Route added.', 'Close', { duration: 3000 }); this.loadConfig(); },
        error: (err) => this.snackBar.open(err?.error?.message || 'Error adding route.', 'Close', { duration: 4000 }),
      }).add(() => this.isSaving.set(false));
    });
  }

  editRoute(route: RouteConfig) {
    if (this.isSaving()) return;
    const ref = this.dialog.open(RouteDialogComponent, {
      width: '750px',
      data: { route, clusters: this.clusterRefs(), existingRoutes: this.routes() },
    });
    ref.afterClosed().subscribe((result: RouteConfig | undefined) => {
      if (!result) return;
      this.isSaving.set(true);
      this.proxyService.updateRoute(route.routeId, result).subscribe({
        next: () => { this.snackBar.open('Route updated.', 'Close', { duration: 3000 }); this.loadConfig(); },
        error: (err) => this.snackBar.open(err?.error?.message || 'Error updating route.', 'Close', { duration: 4000 }),
      }).add(() => this.isSaving.set(false));
    });
  }

  deleteRoute(route: RouteConfig) {
    if (this.isSaving()) return;
    const ref = this.dialog.open(ConfirmDialogComponent, {
      width: '380px',
      data: { title: 'Delete Route', message: `Delete route "${route.routeId}"?`, confirmLabel: 'Delete' },
    });
    ref.afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) return;
      this.isSaving.set(true);
      this.proxyService.deleteRoute(route.routeId).subscribe({
        next: () => {
          this.snackBar.open('Route deleted.', 'Close', { duration: 3000 });
          this.routes.update(rs => rs.filter(r => r.routeId !== route.routeId));
        },
        error: (err) => this.snackBar.open(err?.error?.message || 'Error deleting route.', 'Close', { duration: 4000 }),
      }).add(() => this.isSaving.set(false));
    });
  }

  rawEditRoute(route: RouteConfig) {
    if (this.isSaving()) return;
    const ref = this.dialog.open(RawEditDialogComponent, {
      width: '600px',
      data: { item: route, label: route.routeId },
    });
    ref.afterClosed().subscribe((result: RouteConfig | undefined) => {
      if (!result) return;
      this.isSaving.set(true);
      this.proxyService.updateRoute(route.routeId, result).subscribe({
        next: () => { this.snackBar.open('Route updated.', 'Close', { duration: 3000 }); this.loadConfig(); },
        error: (err) => this.snackBar.open(err?.error?.message || 'Error updating route.', 'Close', { duration: 4000 }),
      }).add(() => this.isSaving.set(false));
    });
  }

  // ── Cluster CRUD ───────────────────────────────────────────────────────────

  addCluster() {
    if (this.isSaving()) return;
    const ref = this.dialog.open(ClusterDialogComponent, {
      width: '750px',
      data: { existingClusters: this.clusters() },
    });
    ref.afterClosed().subscribe((result: ClusterConfig | undefined) => {
      if (!result) return;
      this.isSaving.set(true);
      this.proxyService.addCluster(result).subscribe({
        next: () => { this.snackBar.open('Cluster added.', 'Close', { duration: 3000 }); this.loadConfig(); },
        error: (err) => this.snackBar.open(err?.error?.message || 'Error adding cluster.', 'Close', { duration: 4000 }),
      }).add(() => this.isSaving.set(false));
    });
  }

  editCluster(cluster: ClusterConfig) {
    if (this.isSaving()) return;
    const ref = this.dialog.open(ClusterDialogComponent, {
      width: '750px',
      data: { cluster, existingClusters: this.clusters() },
    });
    ref.afterClosed().subscribe((result: ClusterConfig | undefined) => {
      if (!result) return;
      this.isSaving.set(true);
      this.proxyService.updateCluster(cluster.clusterId, result).subscribe({
        next: () => { this.snackBar.open('Cluster updated.', 'Close', { duration: 3000 }); this.loadConfig(); },
        error: (err) => this.snackBar.open(err?.error?.message || 'Error updating cluster.', 'Close', { duration: 4000 }),
      }).add(() => this.isSaving.set(false));
    });
  }

  deleteCluster(cluster: ClusterConfig) {
    if (this.isSaving()) return;
    const ref = this.dialog.open(ConfirmDialogComponent, {
      width: '380px',
      data: { title: 'Delete Cluster', message: `Delete cluster "${cluster.clusterId}"?`, confirmLabel: 'Delete' },
    });
    ref.afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) return;
      this.isSaving.set(true);
      this.proxyService.deleteCluster(cluster.clusterId).subscribe({
        next: () => {
          this.snackBar.open('Cluster deleted.', 'Close', { duration: 3000 });
          this.clusters.update(cs => cs.filter(c => c.clusterId !== cluster.clusterId));
        },
        error: (err) => this.snackBar.open(err?.error?.message || 'Error deleting cluster.', 'Close', { duration: 4000 }),
      }).add(() => this.isSaving.set(false));
    });
  }

  rawEditCluster(cluster: ClusterConfig) {
    if (this.isSaving()) return;
    const ref = this.dialog.open(RawEditDialogComponent, {
      width: '600px',
      data: { item: cluster, label: cluster.clusterId },
    });
    ref.afterClosed().subscribe((result: ClusterConfig | undefined) => {
      if (!result) return;
      this.isSaving.set(true);
      this.proxyService.updateCluster(cluster.clusterId, result).subscribe({
        next: () => { this.snackBar.open('Cluster updated.', 'Close', { duration: 3000 }); this.loadConfig(); },
        error: (err) => this.snackBar.open(err?.error?.message || 'Error updating cluster.', 'Close', { duration: 4000 }),
      }).add(() => this.isSaving.set(false));
    });
  }
}
