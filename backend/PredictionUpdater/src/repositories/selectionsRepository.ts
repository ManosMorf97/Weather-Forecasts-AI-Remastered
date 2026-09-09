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
    // Every citySite at least one user has in their profile, with its service and city.
    // (A citySite row is unique per (city, service), so two users picking the same one
    // collapse to a single row - no dedup needed.)
    const rows = await this.db.citySite.findMany({
      where: { userCitySites: { some: {} } },
      select: {
        citySiteId: true,
        service: { select: { serviceId: true, name: true } },
        city: { select: { cityId: true, name: true, country: true, latitude: true, longitude: true } },
      },
    });

    // Group the flat rows by service.
    const byService = new Map<number, ServiceSelection>();
    for (const cs of rows) {
      let entry = byService.get(cs.service.serviceId);
      if (!entry) {
        entry = { serviceId: cs.service.serviceId, serviceName: cs.service.name, cities: [] };
        byService.set(cs.service.serviceId, entry);
      }
      entry.cities.push({
        cityId: cs.city.cityId,
        citySiteId: cs.citySiteId,
        name: cs.city.name,
        country: cs.city.country,
        latitude: cs.city.latitude.toNumber(),
        longitude: cs.city.longitude.toNumber(),
      });
    }
    return [...byService.values()];
  }
}
