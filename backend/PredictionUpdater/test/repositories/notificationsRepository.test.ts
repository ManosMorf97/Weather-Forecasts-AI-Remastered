import { afterAll, afterEach, beforeAll, describe, expect, it } from 'vitest';
import type { PrismaClient } from '../../src/generated/prisma/client.js';
import { NotificationsRepository } from '../../src/repositories/notificationsRepository.js';
import { startTestDb, truncateAll, type TestDb } from '../support/testDb.js';

let db: TestDb | undefined;
let prisma: PrismaClient;

beforeAll(async () => {
  db = await startTestDb();
  prisma = db.prisma;
}, 300_000);

afterAll(async () => {
  await db?.stop();
});

afterEach(async () => {
  await truncateAll(prisma);
});

// One dangerous CURRENT forecast for a fresh Athens/Open-Meteo citySite, last touched at
// `retrievedAt` (the instant store.ts would have written on insert or on a value change).
async function seedForecast(retrievedAt: Date) {
  const service = await prisma.forecastingService.create({
    data: { name: 'Open-Meteo', apiEndpoint: 'https://example.test' },
  });
  const city = await prisma.city.create({
    data: { name: 'Athens', country: 'Greece', latitude: '37.98380000', longitude: '23.72750000' },
  });
  const citySite = await prisma.citySite.create({
    data: { cityId: city.cityId, serviceId: service.serviceId },
  });
  const forecast = await prisma.forecast.create({
    data: {
      citySiteId: citySite.citySiteId,
      timestamp: new Date('2026-09-10T12:00:00Z'),
      type: 'CURRENT',
      temperature: '35.0',
      humidity: '20.00',
      windSpeed: '10.00',
      dangerFlag: true,
      offsetMinutes: 180,
      retrievedAt,
    },
  });
  return forecast;
}

async function seedUser(userId: string) {
  return prisma.user.create({ data: { userId, createdAt: new Date() } });
}
//CHECK IT

// Two dangerous forecasts (CURRENT and HOURLY) on one fresh citySite, each with its own
// `retrievedAt` - so a test can prove getNotifiedUsersByForecast treats forecasts independently.
async function seedTwoForecasts(retrievedAtA: Date, retrievedAtB: Date) {
  const service = await prisma.forecastingService.create({
    data: { name: 'Open-Meteo', apiEndpoint: 'https://example.test' },
  });
  const city = await prisma.city.create({
    data: { name: 'Athens', country: 'Greece', latitude: '37.98380000', longitude: '23.72750000' },
  });
  const citySite = await prisma.citySite.create({
    data: { cityId: city.cityId, serviceId: service.serviceId },
  });
  const base = {
    citySiteId: citySite.citySiteId,
    temperature: '35.0',
    humidity: '20.00',
    windSpeed: '10.00',
    dangerFlag: true,
    offsetMinutes: 180,
  };
  const forecastA = await prisma.forecast.create({
    data: { ...base, timestamp: new Date('2026-09-10T12:00:00Z'), type: 'CURRENT', retrievedAt: retrievedAtA },
  });
  const forecastB = await prisma.forecast.create({
    data: { ...base, timestamp: new Date('2026-09-10T13:00:00Z'), type: 'HOURLY', retrievedAt: retrievedAtB },
  });
  return { forecastA, forecastB };
}

describe('NotificationsRepository.getNotifiedUsersByForecast', () => {
  it('counts a Sent notification sent at/after the forecast was last updated', async () => {
    const retrievedAt = new Date('2026-09-10T09:00:00Z');
    const forecast = await seedForecast(retrievedAt);
    const alice = await seedUser('alice');
    await prisma.notification.create({
      data: {
        userId: alice.userId,
        forecastId: forecast.forecastId,
        channel: 'email',
        status: 'Sent',
        sentAt: new Date('2026-09-10T09:30:00Z'), // after the update
      },
    });

    const repo = new NotificationsRepository(prisma);
    const result = await repo.getNotifiedUsersByForecast([
      { forecastId: forecast.forecastId, retrievedAt },
    ]);

    expect([...(result.get(forecast.forecastId) ?? [])]).toEqual(['alice']);
  });

  it('does not count a Sent notification from before the forecast was last updated', async () => {
    const retrievedAt = new Date('2026-09-10T09:00:00Z'); // the forecast changed at this instant
    const forecast = await seedForecast(retrievedAt);
    const bob = await seedUser('bob');
    await prisma.notification.create({
      data: {
        userId: bob.userId,
        forecastId: forecast.forecastId,
        channel: 'email',
        status: 'Sent',
        sentAt: new Date('2026-09-10T08:00:00Z'), // stale - predates the update
      },
    });

    const repo = new NotificationsRepository(prisma);
    const result = await repo.getNotifiedUsersByForecast([
      { forecastId: forecast.forecastId, retrievedAt },
    ]);

    expect(result.has(forecast.forecastId)).toBe(false);
  });

  it('does not count a Failed delivery as notified, regardless of timing', async () => {
    const retrievedAt = new Date('2026-09-10T09:00:00Z');
    const forecast = await seedForecast(retrievedAt);
    const carol = await seedUser('carol');
    await prisma.notification.create({
      data: {
        userId: carol.userId,
        forecastId: forecast.forecastId,
        channel: 'email',
        status: 'Failed',
        sentAt: new Date('2026-09-10T09:30:00Z'), // after the update, but it never delivered
      },
    });

    const repo = new NotificationsRepository(prisma);
    const result = await repo.getNotifiedUsersByForecast([
      { forecastId: forecast.forecastId, retrievedAt },
    ]);

    expect(result.has(forecast.forecastId)).toBe(false);
  });

  it('returns nothing for a forecast nobody has been notified about', async () => {
    const retrievedAt = new Date('2026-09-10T09:00:00Z');
    const forecast = await seedForecast(retrievedAt);

    const repo = new NotificationsRepository(prisma);
    const result = await repo.getNotifiedUsersByForecast([
      { forecastId: forecast.forecastId, retrievedAt },
    ]);

    expect(result.size).toBe(0);
  });

  it('re-notifies only the user whose send predates an escalation, on one forecast', async () => {
    const retrievedAt = new Date('2026-09-10T09:00:00Z'); // the forecast just escalated
    const forecast = await seedForecast(retrievedAt);
    const dave = await seedUser('dave'); // notified before the escalation -> due for a re-notify
    const erin = await seedUser('erin'); // notified after the escalation -> already covered
    await prisma.notification.createMany({
      data: [
        {
          userId: dave.userId,
          forecastId: forecast.forecastId,
          channel: 'email',
          status: 'Sent',
          sentAt: new Date('2026-09-10T08:00:00Z'),
        },
        {
          userId: erin.userId,
          forecastId: forecast.forecastId,
          channel: 'email',
          status: 'Sent',
          sentAt: new Date('2026-09-10T09:30:00Z'),
        },
      ],
    });

    const repo = new NotificationsRepository(prisma);
    const result = await repo.getNotifiedUsersByForecast([
      { forecastId: forecast.forecastId, retrievedAt },
    ]);

    expect([...(result.get(forecast.forecastId) ?? [])]).toEqual(['erin']);
    expect(result.get(forecast.forecastId)?.size).toBe(1);
  });

  //Check IT
  it('keeps forecasts independent across two users and two forecasts', async () => {
    const retrievedAtA = new Date('2026-09-10T09:00:00Z'); // forecast A escalated here
    const retrievedAtB = new Date('2026-09-10T10:00:00Z'); // forecast B escalated here
    const { forecastA, forecastB } = await seedTwoForecasts(retrievedAtA, retrievedAtB);
    const frank = await seedUser('frank');
    const grace = await seedUser('grace');

    await prisma.notification.createMany({
      data: [
        // frank: notified for A before it escalated -> stale, due for a re-notify on A
        {
          userId: frank.userId,
          forecastId: forecastA.forecastId,
          channel: 'email',
          status: 'Sent',
          sentAt: new Date('2026-09-10T08:00:00Z'),
        },
        // frank: notified for B after it escalated -> still covered on B
        {
          userId: frank.userId,
          forecastId: forecastB.forecastId,
          channel: 'email',
          status: 'Sent',
          sentAt: new Date('2026-09-10T10:30:00Z'),
        },
        // grace: notified for A after it escalated -> still covered on A
        {
          userId: grace.userId,
          forecastId: forecastA.forecastId,
          channel: 'email',
          status: 'Sent',
          sentAt: new Date('2026-09-10T09:30:00Z'),
        },
        // grace: never notified for B at all
      ],
    });

    const repo = new NotificationsRepository(prisma);
    const result = await repo.getNotifiedUsersByForecast([
      { forecastId: forecastA.forecastId, retrievedAt: retrievedAtA },
      { forecastId: forecastB.forecastId, retrievedAt: retrievedAtB },
    ]);

    // frank's send for A predates A's escalation, so A only still covers grace.
    expect([...(result.get(forecastA.forecastId) ?? [])]).toEqual(['grace']);
    // grace was never sent B, so B only still covers frank.
    expect([...(result.get(forecastB.forecastId) ?? [])]).toEqual(['frank']);
  });
});
