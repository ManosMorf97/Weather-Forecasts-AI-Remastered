import { beforeEach, describe, expect, it, vi } from 'vitest';
import { UnauthorizedError } from './profileApi';
import { requestAnalytics } from './analyticsApi';

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

describe('requestAnalytics', () => {
  const cityIds = [1, 2];
  const serviceIds = [3];
  const dateRangeStart = '2026-01-01';
  const dateRangeEnd = '2026-01-31';

  it('POSTs the request and returns the batch id', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse({ batchId: 'batch-1' }));

    const batchId = await requestAnalytics('jwt-token', cityIds, serviceIds, dateRangeStart, dateRangeEnd);

    expect(batchId).toBe('batch-1');
    expect(fetch).toHaveBeenCalledWith(
      expect.stringMatching(/\/api\/Analytics$/),
      expect.objectContaining({
        method: 'POST',
        headers: { Authorization: 'Bearer jwt-token', 'Content-Type': 'application/json' },
        body: JSON.stringify({ cityIds, serviceIds, dateRangeStart, dateRangeEnd }),
      }),
    );
  });

  it('E2: throws UnauthorizedError on a 401', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, false, 401));

    await expect(
      requestAnalytics('jwt-token', cityIds, serviceIds, dateRangeStart, dateRangeEnd),
    ).rejects.toBeInstanceOf(UnauthorizedError);
  });

  it('A1: surfaces the ProblemDetails detail message on a 400', async () => {
    vi.mocked(fetch).mockResolvedValue(
      jsonResponse({ detail: 'Start must be on or before end, and the range must not exceed 366 days.' }, false, 400),
    );

    await expect(requestAnalytics('jwt-token', cityIds, serviceIds, dateRangeStart, dateRangeEnd)).rejects.toThrow(
      'Start must be on or before end, and the range must not exceed 366 days.',
    );
  });

  it('falls back to a generic message when the body has no detail', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, false, 500));

    await expect(requestAnalytics('jwt-token', cityIds, serviceIds, dateRangeStart, dateRangeEnd)).rejects.toThrow(
      'Failed to request analytics (status 500)',
    );
  });
});
