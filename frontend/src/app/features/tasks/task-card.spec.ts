import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TaskCard } from './task-card';
import { Task } from './task.models';

const baseTask: Task = {
  id: '1',
  title: 'Prepare demo',
  description: 'Seed data and slides',
  status: 'Pending',
  dueDate: '2026-10-05',
  isOverdue: false,
  createdAtUtc: '2026-10-01T00:00:00Z',
  updatedAtUtc: '2026-10-01T00:00:00Z',
};

describe('TaskCard', () => {
  let fixture: ComponentFixture<TaskCard>;

  async function render(task: Task): Promise<HTMLElement> {
    fixture = TestBed.createComponent(TaskCard);
    fixture.componentRef.setInput('task', task);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders title, description and the due date as a local calendar date', async () => {
    const element = await render(baseTask);

    expect(element.querySelector('.title')?.textContent).toContain('Prepare demo');
    expect(element.querySelector('.description')?.textContent).toContain('Seed data and slides');
    expect(element.querySelector('.due')?.textContent).toContain('Oct 5, 2026');
  });

  it('highlights overdue tasks', async () => {
    const element = await render({ ...baseTask, isOverdue: true });

    expect(element.classList).toContain('overdue');
    expect(element.querySelector('.due')?.textContent).toContain('Overdue');
  });

  it('emits edit and delete intents with the task', async () => {
    const element = await render(baseTask);
    const edited: Task[] = [];
    const deleted: Task[] = [];
    fixture.componentInstance.edit.subscribe((t) => edited.push(t));
    fixture.componentInstance.delete.subscribe((t) => deleted.push(t));

    (element.querySelector('[aria-label="Edit Prepare demo"]') as HTMLButtonElement).click();
    (element.querySelector('[aria-label="Delete Prepare demo"]') as HTMLButtonElement).click();

    expect(edited).toEqual([baseTask]);
    expect(deleted).toEqual([baseTask]);
  });

  it('renders user content as text, not HTML', async () => {
    const element = await render({ ...baseTask, title: '<img src=x onerror=alert(1)>' });

    expect(element.querySelector('.title img')).toBeNull();
    expect(element.querySelector('.title')?.textContent).toContain('<img');
  });
});
