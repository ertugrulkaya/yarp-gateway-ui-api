// Proxy.UI/src/app/pages/dashboard/dashboard.ts
import { Component, OnInit, OnDestroy, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatSnackBarModule, MatSnackBar } from '@angular/material/snack-bar';
import { Router, NavigationEnd } from '@angular/router';
import { Subscription } from 'rxjs';
import { filter } from 'rxjs/operators';
import { ProxyConfigService, RouteConfig, ClusterConfig } from '../../services/proxy-config';
import { RouteDialogComponent } from './dialogs/route-dialog';
import { ClusterDialogComponent } from './dialogs/cluster-dialog';
import { RawEditDialogComponent } from './dialogs/raw-edit-dialog';
import { ServiceWizardDialogComponent } from './dialogs/service-wizard-dialog';

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
  ],
  templateUrl: './dashboard.html',
  styleUrls: ['./dashboard.css'],
})
export class DashboardComponent implements OnInit, OnDestroy {
  proxyService = inject(ProxyConfigService);
  dialog = inject(MatDialog);
  snackBar = inject(MatSnackBar);
  router = inject(Router);
  private cdr = inject(ChangeDetectorRef);

  private routerSub?: Subscription;

  routes: RouteConfig[] = [];
  clusters: ClusterConfig[] = [];
  routesColumns = ['RouteId', 'ClusterId', 'Match', 'Actions'];
  clustersColumns = ['ClusterId', 'Destinations', 'Actions'];

  ngOnInit() {
    this.loadConfig();
    this.routerSub = this.router.events.pipe(
      filter(e => e instanceof NavigationEnd && e.urlAfterRedirects.includes('/dashboard'))
    ).subscribe(() => this.loadConfig());
  }

  ngOnDestroy() {
    this.routerSub?.unsubscribe();
  }

  loadConfig() {
    this.proxyService.loadAll().subscribe({
      next: ({ routes, clusters }) => {
        this.routes = routes;
        this.clusters = clusters;
        this.cdr.markForCheck();
      },
      error: () => this.snackBar.open('Error loading configuration.', 'Close', { duration: 3000 }),
    });
  }

  // ── Service Wizard (bulk /raw) ─────────────────────────────────────────────

  openServiceWizard() {
    const dialogRef = this.dialog.open(ServiceWizardDialogComponent, {
      width: '600px',
      data: { existingClusters: this.clusters },
    });
    dialogRef.afterClosed().subscribe((result: { routes: RouteConfig[]; cluster: ClusterConfig } | undefined) => {
      if (!result) return;
      const payload = {
        routes: [...this.routes, ...result.routes],
        clusters: [...this.clusters, result.cluster],
      };
      this.proxyService.updateRawConfig(payload).subscribe({
        next: () => {
          this.snackBar.open('Service added!', 'Close', { duration: 3000 });
          this.loadConfig();
        },
        error: () => this.snackBar.open('Error saving configuration.', 'Close', { duration: 3000 }),
      });
    });
  }

  // ── Route CRUD ─────────────────────────────────────────────────────────────

  addRoute() {
    const dialogRef = this.dialog.open(RouteDialogComponent, {
      width: '750px',
      data: { clusters: this.clusters, existingRoutes: this.routes },
    });
    dialogRef.afterClosed().subscribe((result: RouteConfig | undefined) => {
      if (!result) return;
      this.proxyService.addRoute(result).subscribe({
        next: () => {
          this.snackBar.open('Route added.', 'Close', { duration: 3000 });
          this.loadConfig();
        },
        error: (err) => this.snackBar.open(err?.error || 'Error adding route.', 'Close', { duration: 4000 }),
      });
    });
  }

  editRoute(route: RouteConfig) {
    const dialogRef = this.dialog.open(RouteDialogComponent, {
      width: '750px',
      data: { route, clusters: this.clusters, existingRoutes: this.routes },
    });
    dialogRef.afterClosed().subscribe((result: RouteConfig | undefined) => {
      if (!result) return;
      this.proxyService.updateRoute(route.routeId, result).subscribe({
        next: () => {
          this.snackBar.open('Route updated.', 'Close', { duration: 3000 });
          this.loadConfig();
        },
        error: (err) => this.snackBar.open(err?.error || 'Error updating route.', 'Close', { duration: 4000 }),
      });
    });
  }

  deleteRoute(route: RouteConfig) {
    if (!confirm(`Delete route "${route.routeId}"?`)) return;
    this.proxyService.deleteRoute(route.routeId).subscribe({
      next: () => {
        this.snackBar.open('Route deleted.', 'Close', { duration: 3000 });
        this.routes = this.routes.filter(r => r.routeId !== route.routeId);
      },
      error: (err) => this.snackBar.open(err?.error || 'Error deleting route.', 'Close', { duration: 4000 }),
    });
  }

  rawEditRoute(route: RouteConfig) {
    const dialogRef = this.dialog.open(RawEditDialogComponent, {
      width: '600px',
      data: { item: route, label: route.routeId },
    });
    dialogRef.afterClosed().subscribe((result: RouteConfig | undefined) => {
      if (!result) return;
      this.proxyService.updateRoute(route.routeId, result).subscribe({
        next: () => {
          this.snackBar.open('Route updated.', 'Close', { duration: 3000 });
          this.loadConfig();
        },
        error: (err) => this.snackBar.open(err?.error || 'Error updating route.', 'Close', { duration: 4000 }),
      });
    });
  }

  // ── Cluster CRUD ───────────────────────────────────────────────────────────

  addCluster() {
    const dialogRef = this.dialog.open(ClusterDialogComponent, {
      width: '750px',
      data: { existingClusters: this.clusters },
    });
    dialogRef.afterClosed().subscribe((result: ClusterConfig | undefined) => {
      if (!result) return;
      this.proxyService.addCluster(result).subscribe({
        next: () => {
          this.snackBar.open('Cluster added.', 'Close', { duration: 3000 });
          this.loadConfig();
        },
        error: (err) => this.snackBar.open(err?.error || 'Error adding cluster.', 'Close', { duration: 4000 }),
      });
    });
  }

  editCluster(cluster: ClusterConfig) {
    const dialogRef = this.dialog.open(ClusterDialogComponent, {
      width: '750px',
      data: { cluster, existingClusters: this.clusters },
    });
    dialogRef.afterClosed().subscribe((result: ClusterConfig | undefined) => {
      if (!result) return;
      this.proxyService.updateCluster(cluster.clusterId, result).subscribe({
        next: () => {
          this.snackBar.open('Cluster updated.', 'Close', { duration: 3000 });
          this.loadConfig();
        },
        error: (err) => this.snackBar.open(err?.error || 'Error updating cluster.', 'Close', { duration: 4000 }),
      });
    });
  }

  deleteCluster(cluster: ClusterConfig) {
    if (!confirm(`Delete cluster "${cluster.clusterId}"?`)) return;
    this.proxyService.deleteCluster(cluster.clusterId).subscribe({
      next: () => {
        this.snackBar.open('Cluster deleted.', 'Close', { duration: 3000 });
        this.clusters = this.clusters.filter(c => c.clusterId !== cluster.clusterId);
      },
      error: (err) => this.snackBar.open(err?.error || 'Error deleting cluster.', 'Close', { duration: 4000 }),
    });
  }

  rawEditCluster(cluster: ClusterConfig) {
    const dialogRef = this.dialog.open(RawEditDialogComponent, {
      width: '600px',
      data: { item: cluster, label: cluster.clusterId },
    });
    dialogRef.afterClosed().subscribe((result: ClusterConfig | undefined) => {
      if (!result) return;
      this.proxyService.updateCluster(cluster.clusterId, result).subscribe({
        next: () => {
          this.snackBar.open('Cluster updated.', 'Close', { duration: 3000 });
          this.loadConfig();
        },
        error: (err) => this.snackBar.open(err?.error || 'Error updating cluster.', 'Close', { duration: 4000 }),
      });
    });
  }
}
