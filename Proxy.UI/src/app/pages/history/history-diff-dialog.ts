import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { ConfigHistoryEntry } from '../../services/proxy-config';

@Component({
  selector: 'app-history-diff-dialog',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ data.action | titlecase }} — {{ data.entityType }}/{{ data.entityId }}</h2>
    <mat-dialog-content>
      <div class="diff-container">
        <div class="diff-panel" *ngIf="data.oldValueJson">
          <div class="diff-label old-label">Before</div>
          <pre class="diff-pre">{{ pretty(data.oldValueJson) }}</pre>
        </div>
        <div class="diff-panel" *ngIf="data.newValueJson">
          <div class="diff-label new-label">After</div>
          <pre class="diff-pre">{{ pretty(data.newValueJson) }}</pre>
        </div>
        <div class="diff-panel" *ngIf="!data.oldValueJson && !data.newValueJson">
          <p>No value data recorded.</p>
        </div>
      </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Close</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .diff-container { display: flex; gap: 16px; min-height: 200px; }
    .diff-panel { flex: 1; display: flex; flex-direction: column; }
    .diff-label { font-size: 12px; font-weight: 600; padding: 4px 8px; border-radius: 4px 4px 0 0; }
    .old-label { background: #fce4e4; color: #c62828; }
    .new-label { background: #e8f5e9; color: #2e7d32; }
    .diff-pre { flex: 1; margin: 0; padding: 12px; background: #fafafa; border: 1px solid #e0e0e0;
                border-radius: 0 0 4px 4px; font-size: 12px; overflow: auto; white-space: pre-wrap; }
    mat-dialog-content { min-width: 600px; }
  `],
})
export class HistoryDiffDialogComponent {
  constructor(@Inject(MAT_DIALOG_DATA) public data: ConfigHistoryEntry) {}

  pretty(json: string | null): string {
    if (!json) return '';
    try { return JSON.stringify(JSON.parse(json), null, 2); }
    catch { return json; }
  }
}
