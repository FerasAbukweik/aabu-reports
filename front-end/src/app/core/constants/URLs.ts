const BASE = 'http://localhost:8080';

export abstract class URLs {
  static readonly baseApi = `${BASE}/api`;
  static readonly notificationHub = `${BASE}/hubs/notification`;
}
