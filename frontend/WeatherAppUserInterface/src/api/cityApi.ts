export interface GeocodedCity {
  name: string;
  country: string;
  latitude: number;
  longitude: number;
}

interface OpenMeteoGeocodingResult {
  name: string;
  country?: string;
  latitude: number;
  longitude: number;
}

interface OpenMeteoGeocodingResponse {
  results?: OpenMeteoGeocodingResult[];
}

const geocodingUrl = 'https://geocoding-api.open-meteo.com/v1/search';

// UC4 step 3: Frontend calls the external City API directly - no backend involvement, no auth.
export async function searchCities(query: string): Promise<GeocodedCity[]> {
  try {
    return await fetchCities(query);
  } catch {
    // E1: retry once before giving up.
    return await fetchCities(query);
  }
}

async function fetchCities(query: string): Promise<GeocodedCity[]> {
  const url = new URL(geocodingUrl);
  url.searchParams.set('name', query);
  url.searchParams.set('count', '10');
  url.searchParams.set('language', 'en');
  url.searchParams.set('format', 'json');

  const response = await fetch(url);
  if (!response.ok) {
    throw new Error(`City search failed (status ${response.status})`);
  }

  const body = (await response.json()) as OpenMeteoGeocodingResponse;
  // A city with no country doesn't fit the required City.Country column, so drop it.
  return (body.results ?? []).filter((result) => result.country) as GeocodedCity[];
}
