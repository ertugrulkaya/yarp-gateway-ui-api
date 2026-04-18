// Proxy.UI/src/app/pages/dashboard/dialogs/cluster-dialog.ts
import { Component, Inject, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AbstractControl, FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ClusterConfig } from '../../../services/proxy-config';

export interface ClusterDialogData {
  cluster?: ClusterConfig;
  existingClusters: ClusterConfig[];
}

@Component({
  selector: 'app-cluster-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatDialogModule, MatTabsModule,
    MatFormFieldModule, MatInputModule, MatButtonModule, MatSelectModule,
    MatCheckboxModule, MatIconModule, MatDividerModule, MatTooltipModule,
  ],
  templateUrl: './cluster-dialog.html',
  styles: [`
    .tab-content { display: flex; flex-direction: column; gap: 12px; padding: 16px 0; }
    .full-width { width: 100%; }
    .row { display: flex; gap: 8px; align-items: flex-start; }
    .row mat-form-field { flex: 1; }
    .section-label { font-weight: 500; color: #555; margin-top: 8px; margin-bottom: 4px; font-size: 13px; }
    .dest-block { border: 1px solid #e0e0e0; border-radius: 4px; padding: 10px; margin-bottom: 8px; }
    .dest-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 6px; }
    .array-row { display: flex; gap: 8px; align-items: center; margin-bottom: 4px; }
    .array-row mat-form-field { flex: 1; }
    mat-dialog-content { min-height: 380px; }
  `],
})
export class ClusterDialogComponent {
  private fb = inject(FormBuilder);
  private dialogRef = inject(MatDialogRef<ClusterDialogComponent>);

  saving = false;
  form: FormGroup;

  constructor(@Inject(MAT_DIALOG_DATA) public data: ClusterDialogData) {
    const c = data.cluster;

    this.form = this.fb.group({
      clusterId: [c?.clusterId ?? '', [
        Validators.required,
        (ctrl: any) => {
          if (!ctrl.value || ctrl.value === c?.clusterId) return null;
          return data.existingClusters.some(x => x.clusterId.toLowerCase() === ctrl.value.toLowerCase())
            ? { uniqueId: true } : null;
        },
      ]],
      loadBalancingPolicy: [c?.loadBalancingPolicy ?? ''],
      destinations: this.fb.array(
        Object.entries(c?.destinations ?? { dest1: { address: '' } }).map(([key, d]) =>
          this.newDestGroup(key, d.address, d.health ?? '')
        )
      ),
      // Session Affinity
      saEnabled: [c?.sessionAffinity?.enabled ?? false],
      saPolicy: [c?.sessionAffinity?.policy ?? 'Cookie'],
      saFailurePolicy: [c?.sessionAffinity?.failurePolicy ?? 'Redistribute'],
      saAffinityKeyName: [c?.sessionAffinity?.affinityKeyName ?? ''],
      saCookiePath: [c?.sessionAffinity?.cookie?.path ?? '/'],
      saCookieDomain: [c?.sessionAffinity?.cookie?.domain ?? ''],
      saCookieHttpOnly: [c?.sessionAffinity?.cookie?.httpOnly ?? true],
      saCookieIsEssential: [c?.sessionAffinity?.cookie?.isEssential ?? false],
      saCookieSameSite: [c?.sessionAffinity?.cookie?.sameSite ?? 'Lax'],
      saCookieSecurePolicy: [c?.sessionAffinity?.cookie?.securePolicy ?? 'SameAsRequest'],
      // Health Check
      hcActiveEnabled: [c?.healthCheck?.active?.enabled ?? false],
      hcActiveInterval: [c?.healthCheck?.active?.interval ?? '00:00:15'],
      hcActiveTimeout: [c?.healthCheck?.active?.timeout ?? '00:00:10'],
      hcActivePolicy: [c?.healthCheck?.active?.policy ?? 'ConsecutiveFailures'],
      hcActivePath: [c?.healthCheck?.active?.path ?? '/health'],
      hcPassiveEnabled: [c?.healthCheck?.passive?.enabled ?? false],
      hcPassivePolicy: [c?.healthCheck?.passive?.policy ?? 'TransportFailureRate'],
      hcPassiveReactivation: [c?.healthCheck?.passive?.reactivationPeriod ?? '00:02:00'],
      // HTTP Client
      httpClientDangerousCert: [c?.httpClient?.dangerousAcceptAnyServerCertificate ?? false],
      httpClientMaxConn: [c?.httpClient?.maxConnectionsPerServer ?? null],
      httpClientHttp1: [c?.httpClient?.enableMultipleHttp1Connections ?? false],
      httpClientHttp2: [c?.httpClient?.enableMultipleHttp2Connections ?? false],
      httpClientReqEncoding: [c?.httpClient?.requestHeaderEncoding ?? ''],
      httpClientResEncoding: [c?.httpClient?.responseHeaderEncoding ?? ''],
      // HTTP Request
      httpReqTimeout: [c?.httpRequest?.activityTimeout ?? ''],
      httpReqVersion: [c?.httpRequest?.version ?? ''],
      httpReqVersionPolicy: [c?.httpRequest?.versionPolicy ?? ''],
      httpReqBuffering: [c?.httpRequest?.allowResponseBuffering ?? false],
      // Metadata
      metadata: this.fb.array(
        Object.entries(c?.metadata ?? {}).map(([k, v]) => this.fb.group({ key: [k], value: [v] }))
      ),
    });
  }

  // ── Accessors ──────────────────────────────────────────────────────────────

  get destinations(): FormArray { return this.form.get('destinations') as FormArray ?? new FormArray<AbstractControl>([]); }
  get metadata(): FormArray { return this.form.get('metadata') as FormArray ?? new FormArray<AbstractControl>([]); }

  // ── Destinations ───────────────────────────────────────────────────────────

  private newDestGroup(key = 'dest1', address = '', health = '') {
    return this.fb.group({
      key: [key, Validators.required],
      address: [address, [Validators.required, Validators.pattern('https?://.*')]],
      health: [health],
    });
  }
  addDestination() { this.destinations.push(this.newDestGroup(`dest${this.destinations.length + 1}`)); }
  removeDestination(i: number) { if (this.destinations.length > 1) this.destinations.removeAt(i); }

  // ── Metadata ───────────────────────────────────────────────────────────────

  addMetadata() { this.metadata.push(this.fb.group({ key: [''], value: [''] })); }
  removeMetadata(i: number) { this.metadata.removeAt(i); }

  // ── Save ───────────────────────────────────────────────────────────────────

  onSave() {
    if (this.form.invalid || this.saving) return;
    this.saving = true;
    const v = this.form.value;

    const result: ClusterConfig = {
      clusterId: v.clusterId,
      loadBalancingPolicy: v.loadBalancingPolicy || undefined,
      destinations: Object.fromEntries(
        v.destinations.map((d: any) => [d.key, {
          address: d.address,
          health: d.health || undefined,
        }])
      ),
      sessionAffinity: v.saEnabled ? {
        enabled: true,
        policy: v.saPolicy || undefined,
        failurePolicy: v.saFailurePolicy || undefined,
        affinityKeyName: v.saAffinityKeyName || undefined,
        cookie: {
          path: v.saCookiePath || undefined,
          domain: v.saCookieDomain || undefined,
          httpOnly: v.saCookieHttpOnly === true ? true : undefined,
          isEssential: v.saCookieIsEssential === true ? true : undefined,
          sameSite: v.saCookieSameSite || undefined,
          securePolicy: v.saCookieSecurePolicy || undefined,
        },
      } : undefined,
      healthCheck: (v.hcActiveEnabled || v.hcPassiveEnabled) ? {
        active: v.hcActiveEnabled ? {
          enabled: true,
          interval: v.hcActiveInterval || undefined,
          timeout: v.hcActiveTimeout || undefined,
          policy: v.hcActivePolicy || undefined,
          path: v.hcActivePath || undefined,
        } : undefined,
        passive: v.hcPassiveEnabled ? {
          enabled: true,
          policy: v.hcPassivePolicy || undefined,
          reactivationPeriod: v.hcPassiveReactivation || undefined,
        } : undefined,
      } : undefined,
      httpClient: (v.httpClientDangerousCert || v.httpClientMaxConn || v.httpClientHttp1 ||
                   v.httpClientHttp2 || v.httpClientReqEncoding || v.httpClientResEncoding) ? {
        dangerousAcceptAnyServerCertificate: v.httpClientDangerousCert === true ? true : undefined,
        maxConnectionsPerServer: v.httpClientMaxConn ?? undefined,
        enableMultipleHttp1Connections: v.httpClientHttp1 === true ? true : undefined,
        enableMultipleHttp2Connections: v.httpClientHttp2 === true ? true : undefined,
        requestHeaderEncoding: v.httpClientReqEncoding || undefined,
        responseHeaderEncoding: v.httpClientResEncoding || undefined,
      } : undefined,
      httpRequest: (v.httpReqTimeout || v.httpReqVersion || v.httpReqVersionPolicy || v.httpReqBuffering) ? {
        activityTimeout: v.httpReqTimeout || undefined,
        version: v.httpReqVersion || undefined,
        versionPolicy: v.httpReqVersionPolicy || undefined,
        allowResponseBuffering: v.httpReqBuffering === true ? true : undefined,
      } : undefined,
      metadata: v.metadata?.length
        ? Object.fromEntries(v.metadata.map((m: any) => [m.key, m.value]))
        : undefined,
    };

    this.dialogRef.close(result);
  }

  onCancel() { this.dialogRef.close(); }
}
