export interface SqlServerParts {
  server: string; // host, host,port, or host\INSTANCE - passed through as-is
  user: string;
  password: string;
  database: string;
}

// Build a Prisma SQL Server connection URL from the same parts WeatherUserActions' Program.cs
// uses (YOUR_SERVER / YOUR_USER / YOUR_PASSWORD). Prisma's format is ';'-separated, not a query
// string, and expects special characters percent-encoded.
export function buildSqlServerUrl(parts: SqlServerParts): string {
  const enc = encodeURIComponent;
  return (
    `sqlserver://${parts.server};` +
    `database=${enc(parts.database)};` +
    `user=${enc(parts.user)};` +
    `password=${enc(parts.password)};` +
    `encrypt=true;trustServerCertificate=true`
  );
}
