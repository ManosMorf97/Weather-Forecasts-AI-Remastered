import type { PrismaClient } from '../../src/generated/prisma/client.js';

// Shared across repository test files - each takes the caller's own `prisma` client rather
// than closing over a module-level one, since every test file starts its own container.
export function seedUser(prisma: PrismaClient, userId: string) {
  return prisma.user.create({ data: { userId, createdAt: new Date() } });
}
