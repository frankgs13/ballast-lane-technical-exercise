import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { Task } from './task.models';
import { TasksStore, sortTasks } from './tasks.store';

function task(overrides: Partial<Task>): Task {
  return {
    id: crypto.randomUUID(),
    title: 'Task',
    description: null,
    status: 'Pending',
    dueDate: '2026-10-10',
    isOverdue: false,
    createdAtUtc: '2026-10-01T00:00:00Z',
    updatedAtUtc: '2026-10-01T00:00:00Z',
    ...overrides,
  };
}

describe('TasksStore', () => {
  let store: TasksStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [TasksStore, provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(TasksStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function loadWith(tasks: Task[]): void {
    store.load();
    http.expectOne('/api/tasks').flush(tasks);
  }

  it('loads tasks and exposes loading state', () => {
    store.load();
    expect(store.loading()).toBe(true);

    http.expectOne('/api/tasks').flush([task({ title: 'A' })]);

    expect(store.loading()).toBe(false);
    expect(store.loaded()).toBe(true);
    expect(store.tasks().length).toBe(1);
  });

  it('exposes a friendly error when loading fails', () => {
    store.load();
    http.expectOne('/api/tasks').flush(null, { status: 0, statusText: 'Unknown Error' });

    expect(store.error()).toContain('Cannot reach the server');
    expect(store.loading()).toBe(false);
  });

  it('counts tasks per status and overdue', () => {
    loadWith([
      task({ status: 'Pending', isOverdue: true }),
      task({ status: 'InProgress' }),
      task({ status: 'Completed' }),
      task({ status: 'Completed' }),
    ]);

    expect(store.counts()).toEqual({ All: 4, Pending: 1, InProgress: 1, Completed: 2, Overdue: 1 });
  });

  it('filters by status and search term (title or description, case-insensitive)', () => {
    loadWith([
      task({ title: 'Write README', status: 'Pending' }),
      task({ title: 'Deploy', description: 'Push the readme to prod', status: 'Completed' }),
      task({ title: 'Unrelated', status: 'Completed' }),
    ]);

    store.search.set('readme');
    expect(store.visibleTasks().map((t) => t.title)).toEqual(['Write README', 'Deploy']);

    store.statusFilter.set('Completed');
    expect(store.visibleTasks().map((t) => t.title)).toEqual(['Deploy']);
  });

  it('adds a created task in due-date order', () => {
    loadWith([task({ title: 'Later', dueDate: '2026-12-01' })]);

    store.create({ title: 'Sooner', description: null, dueDate: '2026-10-05' }).subscribe();
    http.expectOne({ method: 'POST', url: '/api/tasks' }).flush(task({ title: 'Sooner', dueDate: '2026-10-05' }));

    expect(store.tasks().map((t) => t.title)).toEqual(['Sooner', 'Later']);
  });

  it('does not change local state when the API rejects a create', () => {
    loadWith([]);

    store.create({ title: '', description: null, dueDate: '2026-10-05' }).subscribe({ error: () => undefined });
    http.expectOne({ method: 'POST', url: '/api/tasks' }).flush(
      { errors: { title: ['Title is required.'] } },
      { status: 400, statusText: 'Bad Request' },
    );

    expect(store.tasks()).toEqual([]);
  });

  it('changeStatus sends a full update and replaces the task', () => {
    const existing = task({ title: 'Ship it', description: 'Details' });
    loadWith([existing]);

    store.changeStatus(existing, 'Completed').subscribe();
    const request = http.expectOne({ method: 'PUT', url: `/api/tasks/${existing.id}` });
    expect(request.request.body).toEqual({
      title: 'Ship it',
      description: 'Details',
      dueDate: existing.dueDate,
      status: 'Completed',
    });
    request.flush({ ...existing, status: 'Completed' });

    expect(store.tasks()[0].status).toBe('Completed');
  });

  it('removes a deleted task', () => {
    const existing = task({});
    loadWith([existing]);

    store.remove(existing.id).subscribe();
    http.expectOne({ method: 'DELETE', url: `/api/tasks/${existing.id}` }).flush(null, { status: 204, statusText: 'No Content' });

    expect(store.tasks()).toEqual([]);
  });

  it('sortTasks orders by due date then creation time without mutating input', () => {
    const input = [
      task({ title: 'B', dueDate: '2026-10-10', createdAtUtc: '2026-10-02T00:00:00Z' }),
      task({ title: 'A', dueDate: '2026-10-10', createdAtUtc: '2026-10-01T00:00:00Z' }),
      task({ title: 'C', dueDate: '2026-10-01' }),
    ];

    expect(sortTasks(input).map((t) => t.title)).toEqual(['C', 'A', 'B']);
    expect(input[0].title).toBe('B');
  });
});
