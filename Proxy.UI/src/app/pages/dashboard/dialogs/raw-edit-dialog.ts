import { Component, Inject, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-raw-edit-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatInputModule,
    MatFormFieldModule,
    MatIconModule
  ],
  template: `
    <h2 mat-dialog-title>
      <mat-icon style="vertical-align: middle; margin-right: 8px;">code</mat-icon>
      Edit as Raw JSON — <code>{{ data.label }}</code>
    </h2>
    <mat-dialog-content>
      <p style="color: #888; font-size: 0.85em; margin: 0 0 12px;">
        Edit the JSON directly. Must be valid JSON. Saving will immediately reload YARP.
      </p>
      <mat-form-field appearance="outline" style="width: 100%;">
        <textarea
          matInput
          rows="18"
          [(ngModel)]="rawJson"
          (ngModelChange)="onJsonChange()"
          style="font-family: monospace; font-size: 13px; white-space: pre;"
        ></textarea>
      </mat-form-field>
      @if (parseError) {
        <p style="color: #f44336; font-size: 0.85em; margin-top: -8px;">
          ⚠ {{ parseError }}
        </p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="onCancel()">Cancel</button>
      <button mat-raised-button color="primary" [disabled]="!!parseError" (click)="onSave()">
        Save & Apply
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    mat-dialog-content { min-width: 520px; }
  `]
})
export class RawEditDialogComponent {
  private dialogRef = inject(MatDialogRef<RawEditDialogComponent>);

  rawJson: string = '';
  parseError: string | null = null;

  constructor(@Inject(MAT_DIALOG_DATA) public data: { item: any; label: string }) {
    this.rawJson = JSON.stringify(data.item, null, 2);
  }

  onJsonChange() {
    try {
      JSON.parse(this.rawJson);
      this.parseError = null;
    } catch (e: any) {
      this.parseError = e.message;
    }
  }

  onSave() {
    try {
      const parsed = JSON.parse(this.rawJson);
      this.dialogRef.close(parsed);
    } catch {
      // shouldn't happen since button is disabled on error
    }
  }

  onCancel() {
    this.dialogRef.close();
  }
}
