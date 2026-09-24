export interface SqlServerParts {
  /** host, "host,port", or "host\INSTANCE" - the .NET `Server=` form (YOUR_DATABASE_SERVER). */
  server: string;
  user: string;
  password: string;
  database: string;
}

/** mssql (Tedious) config - the subset @prisma/adapter-mssql needs. */
export interface MssqlConfig {
  server: string;
  port?: number;
  database: string;
  user: string;
  password: string;
  options: {
    encrypt: true;
    trustServerCertificate: true;
    instanceName?: string;
  };
}

interface ServerAddress {
  host: string;
  port?: number;
  instanceName?: string;
}

// Split the .NET `Server=` value into parts Prisma's URL parser and the mssql driver each want
// separately (not the comma/backslash form). Handles "host", "host,1433" (port),
// "host\SQLEXPRESS" (named instance) and the combined "host\SQLEXPRESS,1433".
//
// `port` and `instanceName` are mutually exclusive in Tedious: a static port connects directly,
// a bare instance name is resolved by the SQL Server Browser. When both are present the port
// wins and the instance name is dropped.
function parseServer(server: string): ServerAddress {
  let raw = server.trim();
  let port: number | undefined;
  let instanceName: string | undefined;

  const comma = raw.lastIndexOf(',');
  if (comma !== -1) {
    const parsed = Number(raw.slice(comma + 1).trim());
    if (!Number.isNaN(parsed)) {
      port = parsed;
      raw = raw.slice(0, comma).trim();
    }
  }

  const backslash = raw.indexOf('\\');
  if (backslash !== -1) {
    if (port === undefined) instanceName = raw.slice(backslash + 1).trim();
    raw = raw.slice(0, backslash).trim();
  }

  return { host: raw, port, instanceName };
}

// Prisma's SQL Server URL is a JDBC-style ';'-separated string, NOT a percent-encoded URI:
// a value containing any of  : \ = ; / [ ] { }  or a space must be wrapped in braces, and a
// literal '}' inside is doubled. (Percent-encoding would be passed through verbatim and break
// auth.)
function quote(value: string): string {
  if (!/[:\\=;/[\]{} ]/.test(value)) return value;
  return `{${value.replace(/}/g, '}}')}}`;
}

// Build a Prisma SQL Server connection URL from the same parts WeatherUserActions' Program.cs
// uses (YOUR_DATABASE_SERVER / YOUR_USER / YOUR_PASSWORD). Only the Prisma CLI (`db pull`) uses this;
// the runtime client connects through buildMssqlConfig + the driver adapter.
export function buildSqlServerUrl(parts: SqlServerParts): string {
  const { host, port, instanceName } = parseServer(parts.server);
  return (
    `sqlserver://${host}${port ? `:${port}` : ''};` +
    `database=${quote(parts.database)};` +
    `user=${quote(parts.user)};` +
    `password=${quote(parts.password)};` +
    (instanceName ? `instanceName=${quote(instanceName)};` : '') +
    `encrypt=true;trustServerCertificate=true`
  );
}

// Build the mssql driver config the Prisma 7 driver adapter takes (Prisma 7 dropped the
// connection-URL form for the runtime client).
export function buildMssqlConfig(parts: SqlServerParts): MssqlConfig {
  const { host, port, instanceName } = parseServer(parts.server);
  return {
    server: host,
    ...(port ? { port } : {}),
    database: parts.database,
    user: parts.user,
    password: parts.password,
    options: {
      encrypt: true,
      trustServerCertificate: true,
      ...(instanceName ? { instanceName } : {}),
    },
  };
}
