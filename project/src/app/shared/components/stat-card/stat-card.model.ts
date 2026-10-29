import { Status } from '../../../core/enums/status.enum';

export interface StatCard {
  label: string;
  value: string;
  /** text next to the value, e.g. "completed requests" */
  unit?: string;
  /** material symbol name for the icon box */
  icon: string;
  /** controls the colors of the whole card */
  status: Status;
  /** small chip next to the label */
  badge?: string;
  /** footer line under the value */
  note?: string;
  /** material symbol shown before the note */
  noteIcon?: string;
  /** show a pulsing dot before the note instead of an icon */
  notePulse?: boolean;
  /** makes the whole card a link */
  route?: string;
}
