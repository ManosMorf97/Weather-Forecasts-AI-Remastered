// NEW TICKET start
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { UnauthorizedError } from './profileApi';
import { saveServices } from './userServicesApi';

function jsonResponse(body: unknown, ok = true, status = 200): Response {
  return {
    ok,
    status,
    json: () => Promise.resolve(body),
  } as Response;
}

beforeEach(() => {
  vi.stubGlobal('fetch', vi.fn());
});

describe('saveServices', () => {
  const serviceIds = [1, 2];

  it('PUTs the service ids as JSON to the UserServices endpoint', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null));

    await saveServices('jwt-token', serviceIds);

    expect(fetch).toHaveBeenCalledWith(
      expect.stringMatching(/\/api\/UserServices$/),
      expect.objectContaining({
        method: 'PUT',
        headers: { Authorization: 'Bearer jwt-token', 'Content-Type': 'application/json' },
        body: JSON.stringify({ serviceIds }),
      }),
    );
  });

  it('E2: throws UnauthorizedError on a 401', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, false, 401));

    await expect(saveServices('jwt-token', serviceIds)).rejects.toBeInstanceOf(UnauthorizedError);
  });

  it('surfaces the ProblemDetails detail message on a 400', async () => {
    vi.mocked(fetch).mockResolvedValue(
      jsonResponse({ detail: 'One or more selected forecasting services do not exist.' }, false, 400),
    );

    await expect(saveServices('jwt-token', serviceIds)).rejects.toThrow(
      'One or more selected forecasting services do not exist.',
    );
  });

  it('falls back to a generic message when the body has no detail', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, false, 500));

    await expect(saveServices('jwt-token', serviceIds)).rejects.toThrow(
      'Failed to save services (status 500)',
    );
  });
});
// NEW TICKET end
