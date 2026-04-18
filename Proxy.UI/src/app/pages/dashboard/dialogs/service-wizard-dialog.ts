import { Component, Inject, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';

@Component({
  selector: 'app-service-wizard-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDividerModule,
    MatIconModule,
    MatChipsModule
  ],
  template: `
    <h2 mat-dialog-title>
      <mat-icon style="vertical-align: middle; margin-right: 8px; color: #1976d2;">auto_awesome</mat-icon>
      Add Service (Template Wizard)
    </h2>

    <mat-dialog-content>
      <p style="color: #666; font-size: 0.88em; margin: 0 0 16px;">
        Fill in the service details below. The wizard will automatically generate standard YARP routes with correct Transforms.
      </p>

      <form [formGroup]="form" class="wizard-form">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Service Prefix</mat-label>
          <input matInput formControlName="prefix" placeholder="e.g. /rfid-loading">
          <mat-hint>All routes will be generated under this path prefix.</mat-hint>
          <mat-error *ngIf="form.get('prefix')?.hasError('pattern')">
            Must start with / and contain no trailing slash. E.g. /my-service
          </mat-error>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width" style="margin-top: 8px;">
          <mat-label>Cluster ID</mat-label>
          <input matInput formControlName="clusterId" placeholder="e.g. rfid-loading">
          <mat-error *ngIf="form.get('clusterId')?.hasError('uniqueId')">
            This Cluster ID already exists.
          </mat-error>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width" style="margin-top: 8px;">
          <mat-label>Destination Address</mat-label>
          <input matInput formControlName="address" placeholder="e.g. http://10.0.0.5:8080">
        </mat-form-field>

        <mat-divider style="margin: 16px 0;"></mat-divider>

        <p style="font-weight: 500; margin: 0 0 8px; font-size: 0.9em;">Route Templates to Generate:</p>

        <div class="route-checkboxes">
          <mat-checkbox formControlName="genApi">
            <span class="route-label"><code>{{ prefix }}/api/&#123;**catch-all&#125;</code></span>
            <span class="route-desc">— API routes (strips prefix)</span>
          </mat-checkbox>
          <mat-checkbox formControlName="genAssets">
            <span class="route-label"><code>{{ prefix }}/assets/&#123;**catch-all&#125;</code></span>
            <span class="route-desc">— Static assets</span>
          </mat-checkbox>
          <mat-checkbox formControlName="genUi">
            <span class="route-label"><code>{{ prefix }}/ui/&#123;**catch-all&#125;</code></span>
            <span class="route-desc">— UI (Angular/React app)</span>
          </mat-checkbox>
          <mat-checkbox formControlName="genRoot">
            <span class="route-label"><code>{{ prefix }}</code></span>
            <span class="route-desc">— Root redirect → /ui/</span>
          </mat-checkbox>
          <mat-checkbox formControlName="genCatchAll">
            <span class="route-label"><code>{{ prefix }}/&#123;**catch-all&#125;</code></span>
            <span class="route-desc">— General catch-all (strips prefix)</span>
          </mat-checkbox>
        </div>

        @if (preview.length > 0) {
          <mat-divider style="margin: 16px 0;"></mat-divider>
          <p style="font-weight: 500; margin: 0 0 8px; font-size: 0.9em; color: #1976d2;">
            <mat-icon style="vertical-align: middle; font-size: 16px;">preview</mat-icon>
            Preview — {{ preview.length }} routes will be created:
          </p>
          <div class="preview-list">
            @for (r of preview; track r.routeId) {
              <div class="preview-item">
                <mat-icon style="font-size: 14px; color: #4caf50; vertical-align: middle;">check_circle</mat-icon>
                <code>{{ r.routeId }}</code>
                <span class="preview-path">{{ r.match.path }}</span>
              </div>
            }
          </div>
        }
      </form>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button (click)="onCancel()">Cancel</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid || preview.length === 0" (click)="onSave()">
        <mat-icon>rocket_launch</mat-icon>
        Generate & Save
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .wizard-form { display:flex; flex-direction:column; gap:4px; width: 520px; }
    .full-width { width: 100%; }
    .route-checkboxes { display:flex; flex-direction:column; gap:8px; }
    .route-label { font-size: 0.88em; }
    .route-desc { font-size: 0.82em; color: #777; margin-left: 4px; }
    .preview-list { background: #f5f5f5; border-radius: 6px; padding: 8px 12px; display:flex; flex-direction:column; gap:4px; }
    .preview-item { display:flex; align-items:center; gap:8px; font-size: 0.85em; }
    .preview-path { color: #888; }
  `]
})
export class ServiceWizardDialogComponent {
  private fb = inject(FormBuilder);
  private dialogRef = inject(MatDialogRef<ServiceWizardDialogComponent>);

  form: FormGroup;
  existingClusters: any[] = [];

  get prefix(): string {
    return this.form?.get('prefix')?.value || '/my-service';
  }

  get preview(): any[] {
    if (this.form.invalid) return [];
    return this.generateRoutes();
  }

  constructor(@Inject(MAT_DIALOG_DATA) public data: { existingClusters: any[] }) {
    this.existingClusters = data.existingClusters || [];

    this.form = this.fb.group({
      prefix:    ['', [Validators.required, Validators.pattern(/^\/[a-z0-9\-_]+(\/[a-z0-9\-_]+)*$/)]],
      clusterId: ['', [Validators.required, this.uniqueClusterValidator()]],
      address:   ['', [Validators.required, Validators.pattern('https?://.*')]],
      genApi:      [true],
      genAssets:   [true],
      genUi:       [true],
      genRoot:     [true],
      genCatchAll: [true],
    });
  }

  uniqueClusterValidator() {
    return (control: any) => {
      const val = control.value;
      if (!val) return null;
      const exists = this.existingClusters.some(c => c.clusterId.toLowerCase() === val.toLowerCase());
      return exists ? { uniqueId: true } : null;
    };
  }

  generateRoutes(): any[] {
    const v = this.form.value;
    const prefix: string = v.prefix;
    const clusterId: string = v.clusterId;
    const serviceName = prefix.replace(/^\//, ''); // strip leading /

    const commonTransforms = (extra: any[] = []) => [
      ...extra,
      { 'RequestHeadersCopy': 'true' },
      { 'RequestHeaderOriginalHost': 'true' },
      { 'RequestHeader': 'X-Forwarded-Prefix', 'Set': prefix }
    ];

    const routes: any[] = [];

    if (v.genApi) routes.push({
      routeId: `${serviceName}-api-absolute`,
      clusterId,
      order: -3,
      match: { path: `${prefix}/api/{**catch-all}` },
      transforms: commonTransforms([{ 'PathRemovePrefix': prefix }])
    });

    if (v.genAssets) routes.push({
      routeId: `${serviceName}-assets-absolute`,
      clusterId,
      order: -2,
      match: { path: `${prefix}/assets/{**catch-all}` },
      transforms: commonTransforms()
    });

    if (v.genUi) routes.push({
      routeId: `${serviceName}-ui-absolute`,
      clusterId,
      order: -1,
      match: { path: `${prefix}/ui/{**catch-all}` },
      transforms: commonTransforms()
    });

    if (v.genRoot) routes.push({
      routeId: `${serviceName}-root`,
      clusterId,
      order: 0,
      match: { path: prefix },
      transforms: commonTransforms([
        { 'PathRemovePrefix': prefix },
        { 'PathSet': `${prefix}/ui/` }
      ])
    });

    if (v.genCatchAll) routes.push({
      routeId: serviceName,
      clusterId,
      order: 1,
      match: { path: `${prefix}/{**catch-all}` },
      transforms: commonTransforms([{ 'PathRemovePrefix': prefix }])
    });

    return routes;
  }

  onSave() {
    const v = this.form.value;
    const routes = this.generateRoutes();
    const cluster = {
      clusterId: v.clusterId,
      destinations: { dest1: { address: v.address } }
    };
    this.dialogRef.close({ routes, cluster });
  }

  onCancel() {
    this.dialogRef.close();
  }
}
