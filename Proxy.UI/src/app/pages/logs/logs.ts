import { Component, OnInit, inject, ViewChild, ChangeDetectionStrategy, ChangeDetectorRef, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { LogsService, LogEntry, LogFilters } from '../../services/logs';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog';

const HTTP_METHODS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD', 'OPTIONS'];

@Component({
  selector: 'app-logs',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatInputModule,
    MatFormFieldModule,
    MatSelectModule,
    MatDialogModule,
  ],
  templateUrl: './logs.html',
  styleUrls: ['./logs.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LogsComponent implements OnInit {
  logsService = inject(LogsService);
  private cdr = inject(ChangeDetectorRef);
  private dialog = inject(MatDialog);

  dataSource = new MatTableDataSource<LogEntry>([]);
  totalLogs = 0;
  pageSize = 20;
  pageIndex = 0;

  readonly httpMethods = HTTP_METHODS;
  readonly filtersOpen = signal(false);

  // Filter state
  filterClusterId = '';
  filterStatusCode: number | null = null;
  filterClientIp = '';
  filterMethod = '';

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  displayedColumns: string[] = [
    'timestamp',
    'method',
    'path',
    'clusterId',
    'destination',
    'statusCode',
    'duration'
  ];

  ngOnInit() {
    this.loadLogs();
  }

  loadLogs() {
    const offset = this.pageIndex * this.pageSize;
    const filters: LogFilters = {};
    if (this.filterClusterId)  filters.clusterId  = this.filterClusterId;
    if (this.filterStatusCode) filters.statusCode = this.filterStatusCode;
    if (this.filterClientIp)   filters.clientIp   = this.filterClientIp;
    if (this.filterMethod)     filters.method     = this.filterMethod;

    this.logsService.getLogs(this.pageSize, offset, filters).subscribe({
      next: (response) => {
        this.dataSource.data = response.data;
        this.totalLogs = response.total;
        this.cdr.markForCheck();
      }
    });
  }

  applyFilters() {
    this.pageIndex = 0;
    this.loadLogs();
  }

  clearFilters() {
    this.filterClusterId = '';
    this.filterStatusCode = null;
    this.filterClientIp = '';
    this.filterMethod = '';
    this.pageIndex = 0;
    this.loadLogs();
  }

  get hasActiveFilters(): boolean {
    return !!(this.filterClusterId || this.filterStatusCode || this.filterClientIp || this.filterMethod);
  }

  onPageChange(event: PageEvent) {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadLogs();
  }

  clearLogs() {
    const ref = this.dialog.open(ConfirmDialogComponent, {
      width: '380px',
      data: { title: 'Clear Logs', message: 'Are you sure you want to clear all logs?', confirmLabel: 'Clear' },
    });
    ref.afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) return;
      this.logsService.clearLogs().subscribe({
        next: () => {
          this.pageIndex = 0;
          this.cdr.markForCheck();
          this.loadLogs();
        }
      });
    });
  }
}
