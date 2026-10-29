export interface SidebarNavItem {
  route: string;
  label: string;
  /** material symbol name */
  icon: string;
  /** number of in-progress reports shown as a badge. Omit for items without a badge, 0 hides it */
  inProgress?: number;
}
