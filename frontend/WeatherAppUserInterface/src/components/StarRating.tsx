const STAR_VALUES = [1, 2, 3, 4, 5];
// Same amber as the weather-brand sun icon in AuthLayout.tsx - stars stay gold regardless of
// theme, rather than tying them to the blue --accent used for buttons/links.
const STAR_FILLED_COLOR = '#fbbf24';

interface StarRatingProps {
  value: number | null;
  onRate: (value: number) => void;
  onClear: () => void;
  disabled?: boolean;
}

// UC7: 5-star rating widget - click a star to rate 1-5, filled up to the current value, with a
// "Remove rating" button shown when a rating already exists. `disabled` also means "a rate/remove
// request for this row is in flight" (see DashboardPage.tsx), so it shows a small spinner too.
export function StarRating({ value, onRate, onClear, disabled = false }: StarRatingProps) {
  return (
    <div className="d-flex align-items-center gap-1">
      {STAR_VALUES.map((star) => {
        const filled = value !== null && star <= value;
        return (
          <button
            key={star}
            type="button"
            className="btn btn-sm p-0 border-0 bg-transparent lh-1"
            style={{ fontSize: '1.25rem', color: filled ? STAR_FILLED_COLOR : 'var(--text)' }}
            aria-label={`Rate ${star} star${star === 1 ? '' : 's'}`}
            aria-pressed={filled}
            disabled={disabled}
            onClick={() => onRate(star)}
          >
            {filled ? '★' : '☆'}
          </button>
        );
      })}

      {disabled && (
        <span className="spinner-border spinner-border-sm ms-1" role="status" style={{ color: 'var(--accent)' }}>
          <span className="visually-hidden">Saving…</span>
        </span>
      )}

      {value !== null && (
        <button
          type="button"
          className="btn btn-sm btn-outline-danger ms-1"
          disabled={disabled}
          onClick={onClear}
        >
          Remove rating
        </button>
      )}
    </div>
  );
}
