import type { Db } from '../db/client.js';
import type { CityInput } from '../providers/types.js';

export interface ServiceSelection {
  serviceId: number;
  serviceName: string;
  cities: CityInput[];
}

// UC11 steps 2-3: the distinct (city, service) pairs users actually have in their profiles
// (UserCitySite -> CitySite), grouped by service. UserService rows (pending picks with no
// city yet) are deliberately ignored - there is nothing to poll without a city.
export class SelectionsRepository {
  constructor(private readonly db: Db) {}

  async getServiceSelections(): Promise<ServiceSelection[]> {
    // Query from the service side so Prisma nests the selected cities under each service -
    // the grouping is the query shape, no manual pass. The `userCitySites: { some: {} }`
    // filter appears twice: once to keep only services someone actually polls, once to keep
    // only the citySites under them that a user selected.
    const services = await this.db.forecastingService.findMany({
      where: { citySites: { some: { userCitySites: { some: {} } } } },
      select: {
        serviceId: true,
        name: true,
        citySites: {
          where: { userCitySites: { some: {} } },
          select: {
            citySiteId: true,
            city: {
              select: { cityId: true, name: true, country: true, latitude: true, longitude: true },
            },
          },
        },
      },
    });

    return services.map((s) => ({
      serviceId: s.serviceId,
      serviceName: s.name,
      cities: s.citySites.map((cs) => ({
        cityId: cs.city.cityId,
        citySiteId: cs.citySiteId,
        name: cs.city.name,
        country: cs.city.country,
        latitude: cs.city.latitude.toNumber(),
        longitude: cs.city.longitude.toNumber(),
      })),
    }));
  }
}
