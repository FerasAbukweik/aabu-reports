import { SidebarNavItem } from './sidebar.model';

export const SIDEBAR_TITLE = 'القائمة الرئيسية';
export const SIDEBAR_ARIA_LABEL = 'شريط التنقل الجانبي';

// TODO: replace with your real items, the order here is the order on screen
export const NAV_ITEMS: SidebarNavItem[] = [
  { route: '/report', label: 'تقديم بلاغ', icon: 'dashboard', exact: true },
  { route: '/history', label: 'سجل البلاغات', icon: 'assignment', inProgress: 0 },
  { route: '/settings', label: 'الإعدادات', icon: 'settings' },
];

export const ADMIN_SIDEBAR_TITLE = 'قائمة المشرف';

// shown instead of NAV_ITEMS while the current route (or one of its parents) has `data: { isAdmin: true }`
export const ADMIN_NAV_ITEMS: SidebarNavItem[] = [
  { route: '/admin/history', label: 'سجل جميع البلاغات', icon: 'assignment' },
  { route: '/admin/buildings', label: 'إدارة مباني الحرم', icon: 'apartment' },
  // way back to the regular user pages
  { route: '/report', label: 'العودة لتقديم بلاغ', icon: 'dashboard', exact: true },
];
