export interface ProfileState {
  hasCitySiteSelection: boolean;
}

export class UnauthorizedError extends Error {
  constructor() {
    super('Not authenticated');
    this.name = 'UnauthorizedError';
  }
}

// Empty in dev, where the Vite proxy forwards /api to WeatherUserActions (see vite.config.ts).
// In prod, frontend and backend are separate services, so YOUR_API_URL must point at the
// backend's origin.
const apiBaseUrl = import.meta.env.YOUR_API_URL ?? '';

// UC2: Create Profile - JIT-provisions the caller's profile row and reports whether they
// have at least one CitySite selection, so the Frontend knows where to redirect them.
export async function createProfile(jwt: string): Promise<ProfileState> {
  const response = await fetch(`${apiBaseUrl}/api/Profile`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${jwt}` },
  });

  if (response.status === 401) {
    throw new UnauthorizedError();
  }
  if (!response.ok) {
    throw new Error(`Failed to create profile (status ${response.status})`);
  }

  return (await response.json()) as ProfileState;
}
