import { ChangeDetectionStrategy, Component, Injector, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Observable, filter, finalize, switchMap } from 'rxjs';

import { AuthService } from '../../core/auth/auth.service';
import { toApiError } from '../../core/http/api-error';
import { ConfirmDialog, ConfirmDialogData } from '../../shared/confirm-dialog';
import { TaskCard } from './task-card';
import { TaskFormDialog, TaskFormDialogData } from './task-form-dialog';
import { TASK_STATUSES, Task, TaskStatus, statusLabel } from './task.models';
import { StatusFilter, TasksStore } from './tasks.store';

/** Container: wires the store, dialogs and notifications to presentational components. */
@Component({
  selector: 'app-task-list-page',
  imports: [
    FormsModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    TaskCard,
  ],
  providers: [TasksStore],
  templateUrl: './task-list-page.html',
  styleUrl: './task-list-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TaskListPage implements OnInit {
  protected readonly store = inject(TasksStore);
  protected readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly injector = inject(Injector);

  protected readonly statuses = TASK_STATUSES;
  /** Ids of tasks with a request in flight, to disable their buttons and prevent double submits. */
  protected readonly busyIds = signal<ReadonlySet<string>>(new Set());

  ngOnInit(): void {
    this.store.load();
  }

  protected setFilter(value: StatusFilter): void {
    this.store.statusFilter.set(value);
  }

  protected openCreate(): void {
    this.openForm({}).subscribe((task) => this.notify(`Task "${task.title}" created`));
  }

  protected openEdit(task: Task): void {
    this.openForm({ task }).subscribe(() => this.notify('Changes saved'));
  }

  protected changeStatus(task: Task, status: TaskStatus): void {
    this.track(task.id, this.store.changeStatus(task, status)).subscribe({
      next: () => this.notify(`Marked as ${statusLabel(status).toLowerCase()}`),
      error: (error: unknown) => this.notify(toApiError(error).message),
    });
  }

  protected confirmDelete(task: Task): void {
    this.dialog
      .open<ConfirmDialog, ConfirmDialogData, boolean>(ConfirmDialog, {
        data: { title: 'Delete task?', message: `"${task.title}" will be permanently deleted.`, confirmText: 'Delete' },
        width: '400px',
      })
      .afterClosed()
      .pipe(
        filter(Boolean),
        switchMap(() => this.track(task.id, this.store.remove(task.id))),
      )
      .subscribe({
        next: () => this.notify('Task deleted'),
        error: (error: unknown) => this.notify(toApiError(error).message),
      });
  }

  private openForm(data: TaskFormDialogData): Observable<Task> {
    return this.dialog
      .open<TaskFormDialog, TaskFormDialogData, Task>(TaskFormDialog, {
        data,
        // The dialog must resolve the page-scoped TasksStore, not a root instance.
        injector: this.injector,
        width: '560px',
        maxWidth: '95vw',
        autoFocus: 'first-tabbable',
      })
      .afterClosed()
      // A bare `mat-dialog-close` (Cancel button) closes with "" rather than undefined.
      .pipe(filter((result): result is Task => typeof result === 'object' && result !== null));
  }

  private track<T>(id: string, request$: Observable<T>): Observable<T> {
    this.busyIds.update((ids) => new Set(ids).add(id));
    return request$.pipe(
      finalize(() =>
        this.busyIds.update((ids) => {
          const next = new Set(ids);
          next.delete(id);
          return next;
        }),
      ),
    );
  }

  private notify(message: string): void {
    this.snackBar.open(message, 'OK', { duration: 3000 });
  }
}
