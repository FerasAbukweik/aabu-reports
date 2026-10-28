/**
 * The values of a numeric enum, in declaration order.
 * A numeric enum also stores the reverse names (0 -> 'Normal'), so only the numbers are kept.
 */
export function getEnumValues<E extends Record<string, string | number>>(
  enumObject: E,
): E[keyof E][] {
  return Object.values(enumObject).filter((value): value is E[keyof E] => typeof value === 'number');
}
