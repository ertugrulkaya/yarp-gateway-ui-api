import { Component, Inject, ChangeDetectionStrategy, ElementRef, ViewChild, AfterViewInit, OnDestroy, CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ConfigHistoryEntry } from '../../services/proxy-config';
import 'json-diff-viewer-component';

@Component({
  selector: 'app-json-diff-viewer-dialog',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule, MatIconModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
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
      <json-diff-viewer #viewer></json-diff-viewer>
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

    :host {
      display: flex;
      flex-direction: column;
    }

    mat-dialog-content {
      flex: 1 1 auto;
      padding: 0 24px 8px;
    }

    json-diff-viewer {
      height: 600px;
      border-radius: 16px;
    }
  `],
})
export class JsonDiffViewerDialogComponent implements AfterViewInit, OnDestroy {
  @ViewChild('viewer') viewer!: ElementRef<HTMLElement & { setData: (left: object, right: object) => void }>;

  constructor(@Inject(MAT_DIALOG_DATA) public data: ConfigHistoryEntry) {}

  ngAfterViewInit(): void {
    const oldValue = this.data.oldValueJson ? JSON.parse(this.data.oldValueJson) : {};
    const newValue = this.data.newValueJson ? JSON.parse(this.data.newValueJson) : {};
    this.viewer.nativeElement.setData(oldValue, newValue);
  }

  ngOnDestroy(): void {}
}