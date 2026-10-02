export type TaskStatus = 'Pending' | 'InProgress' | 'Completed';

export const TASK_STATUSES: readonly { value: TaskStatus; label: string; icon: string }[] = [
  { value: 'Pending', label: 'Pending', icon: 'radio_button_unchecked' },
  { value: 'InProgress', label: 'In progress', icon: 'timelapse' },
  { value: 'Completed', label: 'Completed', icon: 'check_circle' },
];

export function statusLabel(status: TaskStatus): string {
  return TASK_STATUSES.find((s) => s.value === status)?.label ?? status;
}

export interface Task {
  id: string;
  title: string;
  description: string | null;
  status: TaskStatus;
  /** Calendar date, "yyyy-MM-dd". */
  dueDate: string;
  isOverdue: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateTaskRequest {
  title: string;
  description: string | null;
  dueDate: string;
}

export interface UpdateTaskRequest extends CreateTaskRequest {
  status: TaskStatus;
}

export const TITLE_MAX_LENGTH = 200;
export const DESCRIPTION_MAX_LENGTH = 2000;
