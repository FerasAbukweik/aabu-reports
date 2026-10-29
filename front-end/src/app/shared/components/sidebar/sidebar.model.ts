export interface SidebarNavItem {
  route: string;
  label: string;
  /** material symbol name */
  icon: string;
  /** number of in-progress reports shown as a badge. Omit for items without a badge, 0 hides it */
  inProgress?: number;
  /** active only on this exact url (not on its children, e.g. /report is the map but /report/:id is a report page) */
  exact?: boolean;
}
