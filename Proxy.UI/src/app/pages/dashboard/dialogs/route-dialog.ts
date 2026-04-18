// Proxy.UI/src/app/pages/dashboard/dialogs/route-dialog.ts
import { Component, Inject, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AbstractControl, FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { COMMA, ENTER } from '@angular/cdk/keycodes';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatChipInputEvent, MatChipsModule } from '@angular/material/chips';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouteConfig } from '../../../services/proxy-config';

export interface RouteDialogData {
  route?: RouteConfig;
  clusters: { clusterId: string }[];
  existingRoutes: RouteConfig[];
}

@Component({
  selector: 'app-route-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatDialogModule, MatTabsModule,
    MatFormFieldModule, MatInputModule, MatButtonModule, MatSelectModule,
    MatCheckboxModule, MatChipsModule, MatIconModule, MatDividerModule,
    MatTooltipModule,
  ],
  templateUrl: './route-dialog.html',
  styles: [`
    .tab-content { display: flex; flex-direction: column; gap: 12px; padding: 16px 0; }
    .full-width { width: 100%; }
    .row { display: flex; gap: 8px; align-items: flex-start; }
    .row mat-form-field { flex: 1; }
    .section-label { font-weight: 500; color: #555; margin-top: 8px; margin-bottom: 4px; font-size: 13px; }
    .array-row { display: flex; gap: 8px; align-items: center; margin-bottom: 4px; }
    .array-row mat-form-field { flex: 1; }
    .preset-row { display: flex; flex-wrap: wrap; gap: 6px; margin-bottom: 12px; }
    .transform-block { border: 1px solid #e0e0e0; border-radius: 4px; padding: 10px; margin-bottom: 8px; }
    .transform-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 6px; font-size: 13px; color: #666; }
    mat-dialog-content { min-height: 380px; }
  `],
})
export class RouteDialogComponent {
  private fb = inject(FormBuilder);
  private dialogRef = inject(MatDialogRef<RouteDialogComponent>);

  readonly separatorKeyCodes = [ENTER, COMMA];
  hosts: string[] = [];
  saving = false;
  form: FormGroup;

  constructor(@Inject(MAT_DIALOG_DATA) public data: RouteDialogData) {
    const r = data.route;
    this.hosts = r?.match?.hosts ? [...r.match.hosts] : [];

    this.form = this.fb.group({
      routeId: [r?.routeId ?? '', [
        Validators.required,
        (ctrl: any) => {
          if (!ctrl.value || ctrl.value === r?.routeId) return null;
          return data.existingRoutes.some(x => x.routeId.toLowerCase() === ctrl.value.toLowerCase())
            ? { uniqueId: true } : null;
        },
      ]],
      clusterId: [r?.clusterId ?? '', Validators.required],
      path: [r?.match?.path ?? '', Validators.required],
      methods: [r?.match?.methods ?? []],
      headers: this.fb.array(
        (r?.match?.headers ?? []).map(h => this.newHeaderGroup(h.name, (h.values ?? []).join(','), h.mode, h.isCaseSensitive))
      ),
      queryParameters: this.fb.array(
        (r?.match?.queryParameters ?? []).map(q => this.newQpGroup(q.name, (q.values ?? []).join(','), q.mode, q.isCaseSensitive))
      ),
      transforms: this.fb.array(
        (r?.transforms ?? []).map(t =>
          this.fb.group({
            entries: this.fb.array(Object.entries(t).map(([k, v]) => this.fb.group({ key: [k], value: [v] })))
          })
        )
      ),
      authorizationPolicy: [r?.authorizationPolicy ?? ''],
      corsPolicy: [r?.corsPolicy ?? ''],
      rateLimiterPolicy: [r?.rateLimiterPolicy ?? ''],
      timeoutPolicy: [r?.timeoutPolicy ?? ''],
      outputCachePolicy: [r?.outputCachePolicy ?? ''],
      maxRequestBodySize: [r?.maxRequestBodySize ?? null],
      order: [r?.order ?? null],
      metadata: this.fb.array(
        Object.entries(r?.metadata ?? {}).map(([k, v]) => this.fb.group({ key: [k], value: [v] }))
      ),
    });
  }

  // ── Accessors ──────────────────────────────────────────────────────────────

  get headers(): FormArray { return this.form.get('headers') as FormArray ?? new FormArray<AbstractControl>([]); }
  get queryParameters(): FormArray { return this.form.get('queryParameters') as FormArray ?? new FormArray<AbstractControl>([]); }
  get transforms(): FormArray { return this.form.get('transforms') as FormArray ?? new FormArray<AbstractControl>([]); }
  get metadata(): FormArray { return this.form.get('metadata') as FormArray ?? new FormArray<AbstractControl>([]); }
  getTransformEntries(i: number): FormArray {
    const group = this.transforms.at(i);
    const fa = group?.get('entries');
    return fa instanceof FormArray ? fa : new FormArray<AbstractControl>([]);
  }

  // ── Hosts (chip list) ──────────────────────────────────────────────────────

  addHost(event: MatChipInputEvent) {
    const v = (event.value ?? '').trim();
    if (v) this.hosts.push(v);
    event.chipInput!.clear();
  }
  removeHost(h: string) { this.hosts = this.hosts.filter(x => x !== h); }

  // ── Headers ────────────────────────────────────────────────────────────────

  private newHeaderGroup(name = '', value = '', mode = 'ExactHeader', cs = false) {
    return this.fb.group({ name: [name], value: [value], mode: [mode], isCaseSensitive: [cs] });
  }
  addHeader() { this.headers.push(this.newHeaderGroup()); }
  removeHeader(i: number) { this.headers.removeAt(i); }

  // ── Query Parameters ───────────────────────────────────────────────────────

  private newQpGroup(name = '', value = '', mode = 'Exact', cs = false) {
    return this.fb.group({ name: [name], value: [value], mode: [mode], isCaseSensitive: [cs] });
  }
  addQueryParameter() { this.queryParameters.push(this.newQpGroup()); }
  removeQueryParameter(i: number) { this.queryParameters.removeAt(i); }

  // ── Transforms ─────────────────────────────────────────────────────────────

  addTransform() {
    this.transforms.push(this.fb.group({
      entries: this.fb.array([this.fb.group({ key: [''], value: [''] })])
    }));
  }

  addPresetTransform(preset: string) {
    const map: Record<string, Array<{ key: string; value: string }>> = {
      pathRemovePrefix:       [{ key: 'PathRemovePrefix', value: '' }],
      pathSet:                [{ key: 'PathSet', value: '' }],
      pathPattern:            [{ key: 'PathPattern', value: '' }],
      requestHeader:          [{ key: 'RequestHeader', value: '' }, { key: 'Set', value: '' }],
      responseHeader:         [{ key: 'ResponseHeader', value: '' }, { key: 'Set', value: '' }],
      requestHeadersCopy:     [{ key: 'RequestHeadersCopy', value: 'true' }],
      requestHeaderOrigHost:  [{ key: 'RequestHeaderOriginalHost', value: 'true' }],
      xForwardedPrefix:       [{ key: 'RequestHeader', value: 'X-Forwarded-Prefix' }, { key: 'Set', value: '' }],
    };
    const entries = (map[preset] ?? []).map(e => this.fb.group({ key: [e.key], value: [e.value] }));
    this.transforms.push(this.fb.group({ entries: this.fb.array(entries) }));
  }

  removeTransform(i: number) { this.transforms.removeAt(i); }
  addTransformEntry(ti: number) { this.getTransformEntries(ti).push(this.fb.group({ key: [''], value: [''] })); }
  removeTransformEntry(ti: number, ei: number) { this.getTransformEntries(ti).removeAt(ei); }

  // ── Metadata ───────────────────────────────────────────────────────────────

  addMetadata() { this.metadata.push(this.fb.group({ key: [''], value: [''] })); }
  removeMetadata(i: number) { this.metadata.removeAt(i); }

  // ── Save ───────────────────────────────────────────────────────────────────

  onSave() {
    if (this.form.invalid || this.saving) return;
    this.saving = true;
    const v = this.form.value;

    const result: RouteConfig = {
      routeId: v.routeId,
      clusterId: v.clusterId || undefined,
      order: v.order ?? undefined,
      match: {
        path: v.path || undefined,
        methods: v.methods?.length ? v.methods : undefined,
        hosts: this.hosts.length ? this.hosts : undefined,
        headers: v.headers?.length
          ? v.headers.map((h: any) => ({
              name: h.name,
              values: h.value ? h.value.split(',').map((s: string) => s.trim()).filter(Boolean) : undefined,
              mode: h.mode || undefined,
              isCaseSensitive: h.isCaseSensitive,
            }))
          : undefined,
        queryParameters: v.queryParameters?.length
          ? v.queryParameters.map((q: any) => ({
              name: q.name,
              values: q.value ? q.value.split(',').map((s: string) => s.trim()).filter(Boolean) : undefined,
              mode: q.mode || undefined,
              isCaseSensitive: q.isCaseSensitive,
            }))
          : undefined,
      },
      transforms: v.transforms?.length
        ? v.transforms.map((t: any) => {
            const dict: Record<string, string> = {};
            t.entries.forEach((e: any) => { if (e.key) dict[e.key] = e.value; });
            return dict;
          }).filter((d: any) => Object.keys(d).length > 0)
        : undefined,
      authorizationPolicy: v.authorizationPolicy || undefined,
      corsPolicy: v.corsPolicy || undefined,
      rateLimiterPolicy: v.rateLimiterPolicy || undefined,
      timeoutPolicy: v.timeoutPolicy || undefined,
      outputCachePolicy: v.outputCachePolicy || undefined,
      maxRequestBodySize: v.maxRequestBodySize ?? undefined,
      metadata: v.metadata?.length
        ? Object.fromEntries(v.metadata.map((m: any) => [m.key, m.value]))
        : undefined,
    };

    this.dialogRef.close(result);
  }

  onCancel() { this.dialogRef.close(); }
}
