import 'dotenv/config';
import { defineConfig } from 'prisma/config';
import { buildSqlServerUrl } from './src/db/connectionUrl.js';

// Prisma 7 reads the datasource URL from here, not schema.prisma. Only CLI commands
// (`prisma db pull` / `db push` / `generate`) use it - the runtime client connects through
// the driver adapter (see src/db/client.ts).
//
// DATABASE_URL wins when set (tests point it at a throwaway container); otherwise the URL is
// assembled from the ambient YOUR_SERVER / YOUR_USER / YOUR_PASSWORD parts, same as the .NET
// side. With none of those present we still hand back a placeholder so `generate` (which does
// not connect) works on a fresh checkout.
function resolveUrl(): string {
  if (process.env.DATABASE_URL) return process.env.DATABASE_URL;

  const { YOUR_SERVER, YOUR_USER, YOUR_PASSWORD, DB_NAME } = process.env;
  if (YOUR_SERVER && YOUR_USER && YOUR_PASSWORD) {
    return buildSqlServerUrl({
      server: YOUR_SERVER,
      user: YOUR_USER,
      password: YOUR_PASSWORD,
      database: DB_NAME ?? 'weather_forecasts_ai',
    });
  }

  return 'sqlserver://placeholder;database=placeholder;user=placeholder;password=placeholder';
}

export default defineConfig({
  schema: 'prisma/schema.prisma',
  datasource: { url: resolveUrl() },
});
