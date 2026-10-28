import { SidebarNavItem } from './sidebar.model';

export const SIDEBAR_TITLE = 'القائمة الرئيسية';
export const SIDEBAR_ARIA_LABEL = 'شريط التنقل الجانبي';

// TODO: replace with your real items, the order here is the order on screen
export const NAV_ITEMS: SidebarNavItem[] = [
  { route: '/report', label: 'تقديم بلاغ', icon: 'dashboard' },
  { route: '/history', label: 'سجل البلاغات', icon: 'assignment', inProgress: 0 },
  { route: '/settings', label: 'الإعدادات', icon: 'settings' },
];
