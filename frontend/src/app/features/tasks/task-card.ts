import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';

import { fromIsoDate } from '../../shared/date-utils';
import { TASK_STATUSES, Task, TaskStatus, statusLabel } from './task.models';

/** Presentational: renders a task and emits user intents. Knows nothing about HTTP or state. */
@Component({
  selector: 'app-task-card',
  imports: [DatePipe, MatButtonModule, MatIconModule, MatMenuModule, MatTooltipModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './task-card.html',
  styleUrl: './task-card.scss',
  host: {
    '[class.completed]': "task().status === 'Completed'",
    '[class.overdue]': 'task().isOverdue',
  },
})
export class TaskCard {
  readonly task = input.required<Task>();
  readonly busy = input(false);

  readonly edit = output<Task>();
  readonly delete = output<Task>();
  readonly statusChange = output<{ task: Task; status: TaskStatus }>();

  protected readonly statuses = TASK_STATUSES;
  protected readonly dueDate = computed(() => fromIsoDate(this.task().dueDate));
  protected readonly statusLabel = computed(() => statusLabel(this.task().status));
  protected readonly statusIcon = computed(
    () => TASK_STATUSES.find((s) => s.value === this.task().status)?.icon ?? 'help',
  );
}
