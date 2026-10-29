/**
 * The values of a numeric enum, in declaration order.
 * A numeric enum also stores the reverse names (0 -> 'Normal'), so only the numbers are kept.
 */
export function getEnumValues<E extends Record<string, string | number>>(
  enumObject: E,
): E[keyof E][] {
  return Object.values(enumObject).filter((value): value is E[keyof E] => typeof value === 'number');
}

/**
 * Reads a numeric enum value that came from the api. The api sends the number (`2`), but a backend that
 * serializes enums as text sends the name (`"InProgress"`, `"in_progress"`): both give the enum member.
 * Anything else gives `fallback`.
 */
export function parseEnum<E extends Record<string, string | number>>(
  enumObject: E,
  value: unknown,
  fallback: E[keyof E],
): E[keyof E] {
  if (typeof value === 'number') {
    return typeof enumObject[enumObject[value] as string] === 'number' ? (value as E[keyof E]) : fallback;
  }

  if (typeof value === 'string') {
    const text = value.trim();
    if (/^\d+$/.test(text)) return parseEnum(enumObject, Number(text), fallback);

    const normalize = (s: string) => s.toLowerCase().replace(/[\s_-]/g, '');
    const key = Object.keys(enumObject).find((k) => normalize(k) === normalize(text));

    if (key !== undefined && typeof enumObject[key] === 'number') return enumObject[key] as E[keyof E];
  }

  return fallback;
}
