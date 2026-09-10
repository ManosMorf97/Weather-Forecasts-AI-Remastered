import { describe, expect, it } from 'vitest';
import { formatLocal } from '../../src/pipeline/notify.js';

describe('formatLocal', () => {
  it('renders a positive offset (Athens summer, UTC+3)', () => {
    expect(formatLocal(new Date('2026-09-09T12:00:00Z'), 180)).toBe(
      '2026-09-09 15:00 (UTC+03:00)',
    );
  });

  it('renders a negative, fractional offset (Newfoundland, UTC-3:30)', () => {
    expect(formatLocal(new Date('2026-09-09T12:00:00Z'), -210)).toBe(
      '2026-09-09 08:30 (UTC-03:30)',
    );
  });

  it('renders zero offset as UTC', () => {
    expect(formatLocal(new Date('2026-09-09T12:00:00Z'), 0)).toBe(
      '2026-09-09 12:00 (UTC+00:00)',
    );
  });

  it('rolls the local date forward when the offset crosses midnight', () => {
    expect(formatLocal(new Date('2026-09-09T23:30:00Z'), 90)).toBe(
      '2026-09-10 01:00 (UTC+01:30)',
    );
  });

  it('rolls the local date backward for a negative offset', () => {
    expect(formatLocal(new Date('2026-09-09T00:30:00Z'), -60)).toBe(
      '2026-09-08 23:30 (UTC-01:00)',
    );
  });
});
