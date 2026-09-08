import { afterAll, afterEach, beforeAll, describe, expect, it } from 'vitest';
import type { PrismaClient } from '../../src/generated/prisma/client.js';
import { SelectionsRepository } from '../../src/repositories/selectionsRepository.js';
import { startTestDb, truncateAll, type TestDb } from '../support/testDb.js';

// Athens / Berlin / Paris coordinates, padded to the Decimal(11,8) column scale.
const ATHENS = { name: 'Athens', country: 'Greece', latitude: '37.98380000', longitude: '23.72750000' };
const BERLIN = { name: 'Berlin', country: 'Germany', latitude: '52.52000000', longitude: '13.40500000' };
const PARIS = { name: 'Paris', country: 'France', latitude: '48.85660000', longitude: '2.35220000' };

let db: TestDb | undefined;
let prisma: PrismaClient;

beforeAll(async () => {
  db = await startTestDb();
  prisma = db.prisma;
}, 300_000);

afterAll(async () => {
  // `db` is undefined if beforeAll threw (e.g. container/push failure) - don't mask that error.
  await db?.stop();
});

afterEach(async () => {
  await truncateAll(prisma);
});

async function seedService(name: string) {
  return prisma.forecastingService.create({
    data: { name, apiEndpoint: `https://example.test/${name}` },
  });
}

describe('SelectionsRepository.getServiceSelections', () => {
  it('groups the cities a user selected under each service', async () => {
    const openMeteo = await seedService('Open-Meteo');
    const openWeatherMap = await seedService('OpenWeatherMap');

    const athens = await prisma.city.create({ data: ATHENS });
    const berlin = await prisma.city.create({ data: BERLIN });

    const athensOpenMeteo = await prisma.citySite.create({
      data: { cityId: athens.cityId, serviceId: openMeteo.serviceId },
    });
    const berlinOpenMeteo = await prisma.citySite.create({
      data: { cityId: berlin.cityId, serviceId: openMeteo.serviceId },
    });
    const athensOpenWeatherMap = await prisma.citySite.create({
      data: { cityId: athens.cityId, serviceId: openWeatherMap.serviceId },
    });

    const user = await prisma.user.create({ data: { userId: 'user-1', createdAt: new Date() } });
    await prisma.userCitySite.createMany({
      data: [
        { userId: user.userId, citySiteId: athensOpenMeteo.citySiteId, addedAt: new Date() },
        { userId: user.userId, citySiteId: berlinOpenMeteo.citySiteId, addedAt: new Date() },
        { userId: user.userId, citySiteId: athensOpenWeatherMap.citySiteId, addedAt: new Date() },
      ],
    });

    const selections = (await new SelectionsRepository(prisma).getServiceSelections())
      .sort((a, b) => a.serviceName.localeCompare(b.serviceName))
      .map((s) => ({ ...s, cities: [...s.cities].sort((a, b) => a.name.localeCompare(b.name)) }));

    expect(selections).toEqual([
      {
        serviceId: openMeteo.serviceId,
        serviceName: 'Open-Meteo',
        // citySiteId is the CitySite row (not the City); lat/lon come back as plain numbers.
        cities: [
          {
            cityId: athens.cityId,
            citySiteId: athensOpenMeteo.citySiteId,
            name: 'Athens',
            country: 'Greece',
            latitude: 37.9838,
            longitude: 23.7275,
          },
          {
            cityId: berlin.cityId,
            citySiteId: berlinOpenMeteo.citySiteId,
            name: 'Berlin',
            country: 'Germany',
            latitude: 52.52,
            longitude: 13.405,
          },
        ],
      },
      {
        serviceId: openWeatherMap.serviceId,
        serviceName: 'OpenWeatherMap',
        cities: [
          {
            cityId: athens.cityId,
            citySiteId: athensOpenWeatherMap.citySiteId,
            name: 'Athens',
            country: 'Greece',
            latitude: 37.9838,
            longitude: 23.7275,
          },
        ],
      },
    ]);
  });

  it('omits citySites no user selected, and services left with none', async () => {
    const openMeteo = await seedService('Open-Meteo');
    const visualCrossing = await seedService('VisualCrossing');

    const athens = await prisma.city.create({ data: ATHENS });
    const paris = await prisma.city.create({ data: PARIS });

    const athensOpenMeteo = await prisma.citySite.create({
      data: { cityId: athens.cityId, serviceId: openMeteo.serviceId },
    });
    // Selected by nobody: another Open-Meteo city, and every VisualCrossing city.
    await prisma.citySite.create({ data: { cityId: paris.cityId, serviceId: openMeteo.serviceId } });
    await prisma.citySite.create({ data: { cityId: athens.cityId, serviceId: visualCrossing.serviceId } });

    const user = await prisma.user.create({ data: { userId: 'user-1', createdAt: new Date() } });
    await prisma.userCitySite.create({
      data: { userId: user.userId, citySiteId: athensOpenMeteo.citySiteId, addedAt: new Date() },
    });

    const selections = await new SelectionsRepository(prisma).getServiceSelections();

    expect(selections).toHaveLength(1);
    expect(selections[0]?.serviceName).toBe('Open-Meteo');
    expect(selections[0]?.cities.map((c) => c.name)).toEqual(['Athens']);
  });

  it('deduplicates a city two users both selected for the same service', async () => {
    const openMeteo = await seedService('Open-Meteo');
    const athens = await prisma.city.create({ data: ATHENS });
    const athensOpenMeteo = await prisma.citySite.create({
      data: { cityId: athens.cityId, serviceId: openMeteo.serviceId },
    });

    const alice = await prisma.user.create({ data: { userId: 'alice', createdAt: new Date() } });
    const bob = await prisma.user.create({ data: { userId: 'bob', createdAt: new Date() } });
    await prisma.userCitySite.createMany({
      data: [
        { userId: alice.userId, citySiteId: athensOpenMeteo.citySiteId, addedAt: new Date() },
        { userId: bob.userId, citySiteId: athensOpenMeteo.citySiteId, addedAt: new Date() },
      ],
    });

    const selections = await new SelectionsRepository(prisma).getServiceSelections();

    expect(selections).toHaveLength(1);
    expect(selections[0]?.cities).toHaveLength(1);
    expect(selections[0]?.cities[0]?.citySiteId).toBe(athensOpenMeteo.citySiteId);
  });

  it('returns an empty array when no user has a city selection', async () => {
    const openMeteo = await seedService('Open-Meteo');
    const athens = await prisma.city.create({ data: ATHENS });
    await prisma.citySite.create({ data: { cityId: athens.cityId, serviceId: openMeteo.serviceId } });

    expect(await new SelectionsRepository(prisma).getServiceSelections()).toEqual([]);
  });
});
