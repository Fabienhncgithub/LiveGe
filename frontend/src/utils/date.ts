const timezonePattern = /(?:Z|[+-]\d{2}:\d{2})$/i

export const parseUtcDate = (value: string) => {
  const utcValue = timezonePattern.test(value) ? value : `${value}Z`
  return new Date(utcValue)
}
