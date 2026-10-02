import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { Observable, finalize } from 'rxjs';

import { applyServerErrors, toApiError } from '../../core/http/api-error';
import { fromIsoDate, startOfToday, toIsoDate } from '../../shared/date-utils';
import {
  DESCRIPTION_MAX_LENGTH,
  TASK_STATUSES,
  TITLE_MAX_LENGTH,
  Task,
  TaskStatus,
} from './task.models';
import { TasksStore } from './tasks.store';

export interface TaskFormDialogData {
  task?: Task;
}

/** Create/edit form. Saves through the store and stays open on errors so the user can fix them. */
@Component({
  selector: 'app-task-form-dialog',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDatepickerModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
  ],
  templateUrl: './task-form-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    form {
      display: flex;
      flex-direction: column;
      gap: 4px;
      padding-top: 8px;
      min-width: min(480px, 80vw);
    }
  `,
})
export class TaskFormDialog {
  private readonly store = inject(TasksStore);
  private readonly dialogRef = inject<MatDialogRef<TaskFormDialog, Task>>(MatDialogRef);
  private readonly existing = inject<TaskFormDialogData>(MAT_DIALOG_DATA).task;

  protected readonly isEdit = this.existing !== undefined;
  protected readonly statuses = TASK_STATUSES;
  protected readonly titleMax = TITLE_MAX_LENGTH;
  protected readonly descriptionMax = DESCRIPTION_MAX_LENGTH;
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  /**
   * New dates cannot be in the past, but an overdue task must stay editable without
   * forcing a new due date (same rule as the API).
   */
  protected readonly minDate = this.existing
    ? new Date(Math.min(startOfToday().getTime(), fromIsoDate(this.existing.dueDate).getTime()))
    : startOfToday();

  protected readonly form = inject(NonNullableFormBuilder).group({
    title: [this.existing?.title ?? '', [Validators.required, Validators.maxLength(TITLE_MAX_LENGTH)]],
    description: [this.existing?.description ?? '', Validators.maxLength(DESCRIPTION_MAX_LENGTH)],
    dueDate: [this.existing ? fromIsoDate(this.existing.dueDate) : startOfToday(), Validators.required],
    status: [this.existing?.status ?? ('Pending' as TaskStatus), Validators.required],
  });

  protected save(): void {
    // Whitespace-only titles pass Validators.required; trim before deciding.
    const title = this.form.controls.title.value.trim();
    if (!title) {
      this.form.controls.title.setErrors({ required: true });
    }

    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    const { description, dueDate, status } = this.form.getRawValue();
    const payload = {
      title,
      description: description.trim() || null,
      dueDate: toIsoDate(dueDate),
    };

    const request$: Observable<Task> = this.existing
      ? this.store.update(this.existing.id, { ...payload, status })
      : this.store.create(payload);

    this.saving.set(true);
    this.error.set(null);
    request$.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: (task) => this.dialogRef.close(task),
      error: (error: unknown) => {
        const apiError = toApiError(error);
        const unmatched = applyServerErrors(this.form, apiError.fieldErrors);
        const hasFieldErrors = Object.keys(apiError.fieldErrors).length > 0;
        this.error.set(unmatched.length ? unmatched.join(' ') : hasFieldErrors ? null : apiError.message);
      },
    });
  }
}
