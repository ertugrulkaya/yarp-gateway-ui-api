// Proxy.UI/src/app/core/raw-editor-guard.ts
import { CanDeactivateFn } from '@angular/router';
import { inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { RawEditorComponent } from '../pages/raw-editor/raw-editor';
import { ConfirmDialogComponent } from '../shared/confirm-dialog/confirm-dialog';

export const rawEditorGuard: CanDeactivateFn<RawEditorComponent> = (component) => {
  if (!component.isDirty()) return true;

  return inject(MatDialog)
    .open(ConfirmDialogComponent, {
      width: '400px',
      data: {
        title: 'Unsaved Changes',
        message: 'You have unsaved changes that will be lost. Leave without saving?',
        confirmLabel: 'Leave',
        cancelLabel: 'Stay',
      },
    })
    .afterClosed();
};
