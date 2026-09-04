import 'dotenv/config';
import { spawnSync } from 'node:child_process';
import { loadConfig } from '../src/config.js';

// Runs the Prisma CLI with DATABASE_URL populated from the assembled YOUR_SERVER / YOUR_USER /
// YOUR_PASSWORD parts, so `prisma db pull` / `prisma generate` work without a hand-written URL.
//   usage: tsx scripts/prisma.ts <prisma args...>
//
// `prisma generate` only needs env("DATABASE_URL") to resolve, not connect - so if the parts
// are missing we still let generate run with a placeholder (db pull will then fail loudly).
let databaseUrl: string;
try {
  databaseUrl = loadConfig().databaseUrl;
} catch {
  databaseUrl = 'sqlserver://placeholder;database=placeholder;user=placeholder;password=placeholder';
}
process.env.DATABASE_URL = databaseUrl;

const result = spawnSync('prisma', process.argv.slice(2), { stdio: 'inherit', shell: true });
process.exit(result.status ?? 1);
