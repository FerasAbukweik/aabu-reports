import { Injectable, signal } from '@angular/core';

export interface MaintenanceStats {
  completed: number;
  completionRate: string;
  inProgress: number;
  avgResponseHours: number;
  fieldCrewNote: string;
}

@Injectable({ providedIn: 'root' })
export class MaintenanceStatsService {
  // TODO: replace with real numbers from the api
  private readonly _stats = signal<MaintenanceStats>({
    completed: 128,
    completionRate: '97.4%',
    inProgress: 4,
    avgResponseHours: 24,
    fieldCrewNote: 'كادر الصيانة متواجد في كلية الهندسة',
  });

  readonly stats = this._stats.asReadonly();
}
