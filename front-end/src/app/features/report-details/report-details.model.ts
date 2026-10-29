export type StepStatus = 'done' | 'current' | 'upcoming' | 'warning';

export interface ProgressStep {
  label: string;
  icon: string;
  status: StepStatus;
  /** small line under the label (the date for the first step) */
  note?: string;
}
