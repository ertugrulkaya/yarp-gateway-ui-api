// Proxy.UI/src/app/pages/history/history-diff-dialog.ts
import { Component, Inject, ChangeDetectionStrategy, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { ConfigHistoryEntry } from '../../services/proxy-config';
import { createTwoFilesPatch } from 'diff';
import { html as diff2html } from 'diff2html';

@Component({
  selector: 'app-history-diff-dialog',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule, MatIconModule, MatButtonToggleModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="diff-dialog-header">
      <h2 mat-dialog-title>
        <span class="action-badge" [class]="data.action">{{ data.action }}</span>
        {{ data.entityType }} / <code>{{ data.entityId }}</code>
      </h2>
      <div class="header-meta">
        <span class="meta-item"><mat-icon inline>person</mat-icon> {{ data.changedBy }}</span>
        <span class="meta-item"><mat-icon inline>schedule</mat-icon> {{ data.changedAt | date:'yyyy-MM-dd HH:mm:ss' }}</span>
      </div>
    </div>

    <mat-dialog-content>
      @if (hasDiff()) {
        <div class="view-toggle">
          <mat-button-toggle-group [value]="viewMode()" (change)="viewMode.set($event.value)">
            <mat-button-toggle value="split">
              <mat-icon>vertical_split</mat-icon> Split
            </mat-button-toggle>
            <mat-button-toggle value="unified">
              <mat-icon>view_stream</mat-icon> Unified
            </mat-button-toggle>
          </mat-button-toggle-group>
        </div>
        <div class="diff-output" [innerHTML]="diffHtml()"></div>
      } @else if (data.newValueJson) {
        <div class="single-panel">
          <div class="panel-label created">Created</div>
          <pre class="json-pre">{{ pretty(data.newValueJson) }}</pre>
        </div>
      } @else if (data.oldValueJson) {
        <div class="single-panel">
          <div class="panel-label deleted">Deleted</div>
          <pre class="json-pre">{{ pretty(data.oldValueJson) }}</pre>
        </div>
      } @else {
        <p class="no-data">No value data recorded for this entry.</p>
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Close</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .diff-dialog-header { padding: 16px 24px 0; }
    h2[mat-dialog-title] {
      display: flex; align-items: center; gap: 8px;
      margin: 0 0 4px; font-size: 1.1rem;
    }
    .action-badge {
      display: inline-block; padding: 2px 8px; border-radius: 12px;
      font-size: 0.75rem; font-weight: 600; text-transform: uppercase;
    }
    .action-badge.create  { background: #e8f5e9; color: #2e7d32; }
    .action-badge.update  { background: #e3f2fd; color: #1565c0; }
    .action-badge.delete  { background: #fce4e4; color: #c62828; }
    .action-badge.restore { background: #fff8e1; color: #f57f17; }
    .header-meta {
      display: flex; gap: 16px; font-size: 0.8rem; color: #666;
      margin-bottom: 8px;
    }
    .meta-item { display: flex; align-items: center; gap: 4px; }
    .meta-item mat-icon { font-size: 14px; width: 14px; height: 14px; }

    .view-toggle { display: flex; justify-content: flex-end; margin-bottom: 8px; }

    mat-dialog-content {
      width: 90vw;
      max-width: 1400px;
      max-height: 90vh;
      overflow: auto;
      padding: 0 24px 8px;
    }

    /* diff2html overrides */
    .diff-output {
      font-size: 13px;
      min-height: 350px;
    }
    .diff-output :global(.d2h-wrapper) { font-size: 13px; }
    .diff-output :global(.d2h-file-header) { display: none; }
    .diff-output :global(.d2h-code-linenumber) { min-width: 40px; }
    .diff-output :global(.d2h-code) { line-height: 1.4; }
    .diff-output :global(.d2h-del) { background: #ffebee; }
    .diff-output :global(.d2h-ins) { background: #e8f5e9; }

    .single-panel { display: flex; flex-direction: column; }
    .panel-label {
      font-size: 12px; font-weight: 600; padding: 4px 10px;
      border-radius: 4px 4px 0 0;
    }
    .panel-label.created { background: #e8f5e9; color: #2e7d32; }
    .panel-label.deleted { background: #fce4e4; color: #c62828; }
    .json-pre {
      margin: 0; padding: 12px; background: #fafafa;
      border: 1px solid #e0e0e0; border-radius: 0 0 4px 4px;
      font-size: 12px; overflow: auto; white-space: pre;
    }
    .no-data { color: #888; padding: 16px 0; }
  `],
})
export class HistoryDiffDialogComponent {
  readonly viewMode = signal<'split' | 'unified'>('split');

  readonly hasDiff = computed(() => !!(this.data.oldValueJson && this.data.newValueJson));

  readonly diffHtml = computed(() => {
    if (!this.hasDiff()) return '';
    const oldJson = this.pretty(this.data.oldValueJson);
    const newJson = this.pretty(this.data.newValueJson);
    const patch = createTwoFilesPatch(
      'Before', 'After',
      oldJson, newJson,
      '', '',
      { context: 4 }
    );
    return diff2html(patch, {
      drawFileList: false,
      matching: 'lines',
      outputFormat: this.viewMode() === 'split' ? 'side-by-side' : 'line-by-line',
    });
  });

  constructor(@Inject(MAT_DIALOG_DATA) public data: ConfigHistoryEntry) {}

  pretty(json: string | null): string {
    if (!json) return '';
    try { return JSON.stringify(JSON.parse(json), null, 2); }
    catch { return json; }
  }
}
