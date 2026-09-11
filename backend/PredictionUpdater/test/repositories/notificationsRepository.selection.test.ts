import { afterAll, afterEach, beforeAll, describe, expect, it } from 'vitest';
import type { PrismaClient } from '../../src/generated/prisma/client.js';
import { NotificationsRepository } from '../../src/repositories/notificationsRepository.js';
import { seedUser } from '../support/seed.js';
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

// Seeds the TC01 matrix: 2 services x 2 cities -> 4 citySites. hank picks Athens/Open-Meteo
// (his own) and Athens/OpenWeatherMap (shared with iris); iris picks the shared one and
// Berlin/OpenWeatherMap (her own); Berlin/Open-Meteo is picked by nobody. The two single-owner
// citySites each get 2 active danger forecasts plus a non-dangerous one; the shared citySite
// gets 1 active + 1 PAST danger forecast; the unselected citySite gets 1 active danger forecast
// despite having no subscribers. Pure "arrange" - the test itself only calls the repository and
// asserts.
async function seedSelectionMatrix() {
  const NOW = new Date('2026-09-11T09:00:00Z');
  const FUTURE = new Date('2026-09-11T12:00:00Z'); // still ahead of NOW -> "active"
  const PAST = new Date('2026-09-11T06:00:00Z'); // already elapsed -> excluded regardless of danger

  const openMeteo = await prisma.forecastingService.create({
    data: { name: 'Open-Meteo', apiEndpoint: 'https://example.test/open-meteo' },
  });
  const openWeatherMap = await prisma.forecastingService.create({
    data: { name: 'OpenWeatherMap', apiEndpoint: 'https://example.test/owm' },
  });
  const athens = await prisma.city.create({
    data: { name: 'Athens', country: 'Greece', latitude: '37.98380000', longitude: '23.72750000' },
  });
  const berlin = await prisma.city.create({
    data: { name: 'Berlin', country: 'Germany', latitude: '52.52000000', longitude: '13.40500000' },
  });

  // The 4 citySites: every (city, service) combination.
  const athensOpenMeteo = await prisma.citySite.create({
    data: { cityId: athens.cityId, serviceId: openMeteo.serviceId },
  });
  const athensOpenWeatherMap = await prisma.citySite.create({
    data: { cityId: athens.cityId, serviceId: openWeatherMap.serviceId },
  });
  const berlinOpenWeatherMap = await prisma.citySite.create({
    data: { cityId: berlin.cityId, serviceId: openWeatherMap.serviceId },
  });
  const berlinOpenMeteoUnselected = await prisma.citySite.create({
    data: { cityId: berlin.cityId, serviceId: openMeteo.serviceId }, // nobody ever picks this one
  });

  const hank = await seedUser(prisma, 'hank');
  const iris = await seedUser(prisma, 'iris');

  await prisma.userCitySite.createMany({
    data: [
      { userId: hank.userId, citySiteId: athensOpenMeteo.citySiteId, addedAt: NOW },
      { userId: hank.userId, citySiteId: athensOpenWeatherMap.citySiteId, addedAt: NOW },
      { userId: iris.userId, citySiteId: athensOpenWeatherMap.citySiteId, addedAt: NOW },
      { userId: iris.userId, citySiteId: berlinOpenWeatherMap.citySiteId, addedAt: NOW },
    ],
  });

  const baseForecast = {
    temperature: '30.0',
    humidity: '40.00',
    windSpeed: '20.00',
    offsetMinutes: 180,
    retrievedAt: NOW,
  };

  // Athens/Open-Meteo (hank only): two active danger forecasts, plus a non-dangerous one that
  // must never surface.
  const athensOpenMeteoCurrentDanger = await prisma.forecast.create({
    data: { ...baseForecast, citySiteId: athensOpenMeteo.citySiteId, timestamp: FUTURE, type: 'CURRENT', dangerFlag: true },
  });
  const athensOpenMeteoHourlyDanger = await prisma.forecast.create({
    data: { ...baseForecast, citySiteId: athensOpenMeteo.citySiteId, timestamp: FUTURE, type: 'HOURLY', dangerFlag: true },
  });
  await prisma.forecast.create({
    data: { ...baseForecast, citySiteId: athensOpenMeteo.citySiteId, timestamp: FUTURE, type: 'DAILY', dangerFlag: false },
  });

  // Athens/OpenWeatherMap (shared by hank and iris): one active danger forecast and one PAST
  // danger forecast - the past one must be excluded even though dangerFlag is true.
  const athensOpenWeatherMapActiveDanger = await prisma.forecast.create({
    data: { ...baseForecast, citySiteId: athensOpenWeatherMap.citySiteId, timestamp: FUTURE, type: 'CURRENT', dangerFlag: true },
  });
  await prisma.forecast.create({
    data: { ...baseForecast, citySiteId: athensOpenWeatherMap.citySiteId, timestamp: PAST, type: 'HOURLY', dangerFlag: true },
  });

  // Berlin/OpenWeatherMap (iris only): two active danger forecasts, plus a non-dangerous one.
  const berlinOpenWeatherMapCurrentDanger = await prisma.forecast.create({
    data: { ...baseForecast, citySiteId: berlinOpenWeatherMap.citySiteId, timestamp: FUTURE, type: 'CURRENT', dangerFlag: true },
  });
  const berlinOpenWeatherMapHourlyDanger = await prisma.forecast.create({
    data: { ...baseForecast, citySiteId: berlinOpenWeatherMap.citySiteId, timestamp: FUTURE, type: 'HOURLY', dangerFlag: true },
  });
  await prisma.forecast.create({
    data: { ...baseForecast, citySiteId: berlinOpenWeatherMap.citySiteId, timestamp: FUTURE, type: 'DAILY', dangerFlag: false },
  });

  // Berlin/Open-Meteo: an active danger forecast, but nobody selected this citySite at all.
  const berlinOpenMeteoUnselectedDanger = await prisma.forecast.create({
    data: { ...baseForecast, citySiteId: berlinOpenMeteoUnselected.citySiteId, timestamp: FUTURE, type: 'CURRENT', dangerFlag: true },
  });

  return {
    NOW,
    hank,
    iris,
    athensOpenMeteo,
    athensOpenWeatherMap,
    berlinOpenWeatherMap,
    berlinOpenMeteoUnselected,
    athensOpenMeteoCurrentDanger,
    athensOpenMeteoHourlyDanger,
    athensOpenWeatherMapActiveDanger,
    berlinOpenWeatherMapCurrentDanger,
    berlinOpenWeatherMapHourlyDanger,
    berlinOpenMeteoUnselectedDanger,
  };
}

// ---- TC01 start ----
describe('NotificationsRepository - selection and danger filtering across a mixed matrix', () => {
  it('TC01: 2 users x 2 cities x 2 services, one past danger forecast, one unselected citySite', async () => {
    const {
      NOW,
      hank,
      iris,
      athensOpenMeteo,
      athensOpenWeatherMap,
      berlinOpenWeatherMap,
      berlinOpenMeteoUnselected,
      athensOpenMeteoCurrentDanger,
      athensOpenMeteoHourlyDanger,
      athensOpenWeatherMapActiveDanger,
      berlinOpenWeatherMapCurrentDanger,
      berlinOpenWeatherMapHourlyDanger,
      berlinOpenMeteoUnselectedDanger,
    } = await seedSelectionMatrix();
    const repo = new NotificationsRepository(prisma);

    // Only the 6 active danger forecasts come back - not the past one, not the non-dangerous ones.
    const dangerForecasts = await repo.getActiveDangerForecasts(NOW);
    expect(dangerForecasts.map((d) => d.forecastId).sort((a, b) => a - b)).toEqual(
      [
        athensOpenMeteoCurrentDanger.forecastId,
        athensOpenMeteoHourlyDanger.forecastId,
        athensOpenWeatherMapActiveDanger.forecastId,
        berlinOpenWeatherMapCurrentDanger.forecastId,
        berlinOpenWeatherMapHourlyDanger.forecastId,
        berlinOpenMeteoUnselectedDanger.forecastId,
      ].sort((a, b) => a - b),
    );

    // Subscribers line up with the selection matrix - the unselected citySite has none at all.
    const subscribers = await repo.getSubscribersByCitySite([
      athensOpenMeteo.citySiteId,
      athensOpenWeatherMap.citySiteId,
      berlinOpenWeatherMap.citySiteId,
      berlinOpenMeteoUnselected.citySiteId,
    ]);
    expect(subscribers.get(athensOpenMeteo.citySiteId)).toEqual(new Set([hank.userId]));
    expect(subscribers.get(athensOpenWeatherMap.citySiteId)).toEqual(new Set([hank.userId, iris.userId]));
    expect(subscribers.get(berlinOpenWeatherMap.citySiteId)).toEqual(new Set([iris.userId]));
    expect(subscribers.has(berlinOpenMeteoUnselected.citySiteId)).toBe(false);

    // Joining the two, as notify.ts does: who actually gets warned about each active forecast.
    const recipientsByForecastId = new Map(
      dangerForecasts.map((d) => [d.forecastId, subscribers.get(d.citySiteId) ?? new Set<string>()]),
    );
    expect(recipientsByForecastId.get(athensOpenMeteoCurrentDanger.forecastId)).toEqual(new Set([hank.userId]));
    expect(recipientsByForecastId.get(athensOpenMeteoHourlyDanger.forecastId)).toEqual(new Set([hank.userId]));
    expect(recipientsByForecastId.get(athensOpenWeatherMapActiveDanger.forecastId)).toEqual(
      new Set([hank.userId, iris.userId]),
    );
    expect(recipientsByForecastId.get(berlinOpenWeatherMapCurrentDanger.forecastId)).toEqual(new Set([iris.userId]));
    expect(recipientsByForecastId.get(berlinOpenWeatherMapHourlyDanger.forecastId)).toEqual(new Set([iris.userId]));
    // dangerous, but unsubscribed
    expect(recipientsByForecastId.get(berlinOpenMeteoUnselectedDanger.forecastId)).toEqual(new Set());
  });
});
// ---- TC01 end ----
