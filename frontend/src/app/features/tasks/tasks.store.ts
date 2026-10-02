import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, finalize, map, tap } from 'rxjs';

import { toApiError } from '../../core/http/api-error';
import { TaskApiService } from './task-api.service';
import { CreateTaskRequest, Task, TaskStatus, UpdateTaskRequest } from './task.models';

export type StatusFilter = TaskStatus | 'All';

/**
 * Signal-based state for the task list page (provided at component level, so it is
 * created and destroyed with the page). Components read signals; only the store mutates them.
 */
@Injectable()
export class TasksStore {
  private readonly api = inject(TaskApiService);

  private readonly _tasks = signal<Task[]>([]);
  private readonly _loading = signal(false);
  private readonly _loaded = signal(false);
  private readonly _error = signal<string | null>(null);

  readonly tasks = this._tasks.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly loaded = this._loaded.asReadonly();
  readonly error = this._error.asReadonly();

  readonly statusFilter = signal<StatusFilter>('All');
  readonly search = signal('');

  readonly counts = computed(() => {
    const tasks = this._tasks();
    return {
      All: tasks.length,
      Pending: tasks.filter((t) => t.status === 'Pending').length,
      InProgress: tasks.filter((t) => t.status === 'InProgress').length,
      Completed: tasks.filter((t) => t.status === 'Completed').length,
      Overdue: tasks.filter((t) => t.isOverdue).length,
    };
  });

  readonly visibleTasks = computed(() => {
    const status = this.statusFilter();
    const term = this.search().trim().toLocaleLowerCase();
    return this._tasks().filter(
      (task) =>
        (status === 'All' || task.status === status) &&
        (!term ||
          task.title.toLocaleLowerCase().includes(term) ||
          (task.description ?? '').toLocaleLowerCase().includes(term)),
    );
  });

  load(): void {
    this._loading.set(true);
    this._error.set(null);
    this.api
      .list()
      .pipe(finalize(() => this._loading.set(false)))
      .subscribe({
        next: (tasks) => {
          this._tasks.set(sortTasks(tasks));
          this._loaded.set(true);
        },
        error: (error: unknown) => this._error.set(toApiError(error).message),
      });
  }

  create(request: CreateTaskRequest): Observable<Task> {
    return this.api.create(request).pipe(tap((task) => this._tasks.update((tasks) => sortTasks([...tasks, task]))));
  }

  update(id: string, request: UpdateTaskRequest): Observable<Task> {
    return this.api.update(id, request).pipe(tap((task) => this.replace(task)));
  }

  changeStatus(task: Task, status: TaskStatus): Observable<Task> {
    return this.update(task.id, {
      title: task.title,
      description: task.description,
      dueDate: task.dueDate,
      status,
    });
  }

  remove(id: string): Observable<void> {
    return this.api.delete(id).pipe(
      tap(() => this._tasks.update((tasks) => tasks.filter((t) => t.id !== id))),
      map(() => undefined),
    );
  }

  private replace(updated: Task): void {
    this._tasks.update((tasks) => sortTasks(tasks.map((t) => (t.id === updated.id ? updated : t))));
  }
}

/** Mirrors the API ordering (due date, then creation time) so local mutations keep the list stable. */
export function sortTasks(tasks: Task[]): Task[] {
  return [...tasks].sort(
    (a, b) => a.dueDate.localeCompare(b.dueDate) || a.createdAtUtc.localeCompare(b.createdAtUtc),
  );
}
