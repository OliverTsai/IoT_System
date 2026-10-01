export function formatDateTime(value: string): string {
  return new Intl.DateTimeFormat("zh-TW", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  }).format(new Date(value));
}

export function formatRelativeTime(value: string): string {
  const differenceSeconds = Math.round((new Date(value).getTime() - Date.now()) / 1_000);
  const formatter = new Intl.RelativeTimeFormat("zh-TW", { numeric: "auto" });
  const absolute = Math.abs(differenceSeconds);

  if (absolute < 60) return formatter.format(differenceSeconds, "second");
  if (absolute < 3_600) return formatter.format(Math.round(differenceSeconds / 60), "minute");
  if (absolute < 86_400) return formatter.format(Math.round(differenceSeconds / 3_600), "hour");
  return formatter.format(Math.round(differenceSeconds / 86_400), "day");
}

export function formatNumber(value: number | null | undefined, digits = 1): string {
  return value === null || value === undefined ? "—" : value.toFixed(digits);
}
