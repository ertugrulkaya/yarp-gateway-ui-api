import { Component, OnInit, inject, ChangeDetectorRef, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { ProxyConfigService, ConfigHistoryEntry } from '../../services/proxy-config';
import { HistoryDiffDialogComponent } from './history-diff-dialog';

@Component({
  selector: 'app-history',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatTooltipModule,
    MatDialogModule,
  ],
  templateUrl: './history.html',
  styleUrls: ['./history.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HistoryComponent implements OnInit {
  private proxyService = inject(ProxyConfigService);
  private cdr = inject(ChangeDetectorRef);
  private dialog = inject(MatDialog);

  entries: ConfigHistoryEntry[] = [];
  total = 0;
  loading = false;
  limit = 100;
  offset = 0;

  columns = ['changedAt', 'action', 'entityType', 'entityId', 'changedBy', 'diff'];

  ngOnInit() { this.load(); }

  load() {
    this.loading = true;
    this.cdr.markForCheck();
    this.proxyService.getHistory(this.limit, this.offset).subscribe({
      next: (res) => {
        this.entries = res.data;
        this.total = res.total;
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: () => { this.loading = false; this.cdr.markForCheck(); },
    });
  }

  actionColor(action: string): string {
    return action === 'create' ? 'primary' : action === 'delete' ? 'warn' : 'accent';
  }

  openDiff(entry: ConfigHistoryEntry) {
    this.dialog.open(HistoryDiffDialogComponent, {
      width: '800px',
      data: entry,
    });
  }

  prevPage() { if (this.offset >= this.limit) { this.offset -= this.limit; this.load(); } }
  nextPage() { if (this.offset + this.limit < this.total) { this.offset += this.limit; this.load(); } }
}
