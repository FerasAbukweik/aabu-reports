const BASE = 'http://localhost:8080';

export abstract class URLs {
  static readonly baseApi = `${BASE}/api`;
  /** the one SignalR hub of the app (notifications and the chat of the reports) */
  static readonly hub = `${BASE}/hubs/app`;
}
