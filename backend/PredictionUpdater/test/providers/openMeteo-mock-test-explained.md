# How `openMeteo.test.ts` mocks the API (the template for all provider tests)

This file was the first provider test written, and `openWeatherMap.test.ts`,
`weatherApi.test.ts` and `visualCrossing.test.ts` all copy its pattern. This doc explains the
mechanics once so you don't have to re-derive them per file.

## The problem: `fetchForCity` calls the real internet

`OpenMeteoProvider.fetchForCity()` calls the global `fetch()`. A test can't let that hit the
real Open-Meteo API — it would be slow, flaky (network down, rate limits), and non-deterministic
(the weather changes every time you run the test). So the test **replaces** `fetch` with a fake
version that returns a canned response we control completely. That's mocking: swap a real,
unpredictable dependency for a fake, predictable one, just for the test.

## The four moving parts

### 1. Freeze time — `vi.useFakeTimers()` + `vi.setSystemTime(NOW)`

The provider's CURRENT/HOURLY/DAILY selection depends on "now" (e.g. "the next 3 hours *from
now*"). If the test let real time run, the expected results would change every time you ran it.
`setSystemTime` pins `Date.now()` / `new Date()` to a fixed instant for the whole test, so the
answer is always the same.

```ts
const NOW = new Date('2026-09-04T12:00:00Z'); // 15:00 Athens local
vi.useFakeTimers();
vi.setSystemTime(NOW);
```

### 2. Fake the network — `vi.stubGlobal('fetch', ...)`

```ts
vi.stubGlobal(
  'fetch',
  vi.fn(async () => new Response(JSON.stringify(buildResponse()), { status: 200 })),
);
```

`vi.stubGlobal('fetch', fn)` replaces the global `fetch` with `fn` for the duration of the test.
`fn` is a `vi.fn()` (a spy — could also assert on how it was called, though this file doesn't).
It returns a *real* `Response` object built from a fake JSON body, so it behaves exactly like a
real `fetch()` result as far as the provider code is concerned: `res.ok` is `true`, `res.json()`
resolves to our fake data. The provider has no idea it isn't talking to the real API.

### 3. Build a realistic fake payload — `buildResponse()`

```ts
function buildResponse() {
  // ...builds `times`, `temps`, `humidity`, `wind` arrays...
  return {
    utc_offset_seconds: 10_800,
    current: { time: '2026-09-04T15:00', temperature_2m: 30.4, ... },
    hourly: { time: times, temperature_2m: temps, ... },
  };
}
```

This is hand-built JSON shaped exactly like what Open-Meteo's real API returns (same field
names Open-Meteo uses), so it passes the provider's zod schema validation and gives the
transform logic (`toForecasts`) something realistic to chew on. Every other provider test
(`weatherApi.test.ts`, etc.) has its own `buildResponse()` shaped like *that* API instead.

### 4. Clean up — `afterEach`

```ts
afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});
```

Without this, the frozen clock and fake `fetch` would leak into the *next* test file, silently
breaking unrelated tests. Every mock you install gets undone after each test.

## Putting it together: one `it()` block traced through

```ts
it('returns one CURRENT reading with normalized units', async () => {
  const provider = new OpenMeteoProvider(15_000);

  const forecasts = await provider.fetchForCity(ATHENS);
  // ^ internally calls the stubbed fetch(), gets buildResponse() back as JSON,
  //   validates it, and runs toForecasts() against NOW.

  const current = forecasts.filter((f) => f.type === 'CURRENT');
  expect(current[0]).toMatchObject({ offsetMinutes: 180, temperatureC: 30.4, ... });
  expect(current[0]?.timestamp.toISOString()).toBe('2026-09-04T12:00:00.000Z');
  // 15:00 Athens (UTC+3, so utc_offset_seconds: 10_800) => 12:00 UTC — worked out by hand
  // from the fake data above, not from any real API call.
});
```

Nothing here touches a real network or a real clock. Every expected value (`30.4`,
`'2026-09-04T12:00:00.000Z'`, etc.) is computed by hand from the fake input, which is what makes
the test deterministic and fast — no need for `beforeAll`/DB setup like the repository tests
use (see `notificationsRepository.test.ts`), because there's no database involved here at all.

## Why this evolved slightly in the later provider tests

`openWeatherMap.test.ts` stubs `fetch` once in `beforeEach` (like this file), because every test
in it uses the same fake response. `weatherApi.test.ts` and `visualCrossing.test.ts` need
*different* fake responses per test (to test danger-flag alerts), so they factor the stub into a
small `stubFetch(body)` helper called individually inside each `it()` instead of `beforeEach`.
Same mocking mechanics, just called at a different point.
