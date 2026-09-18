import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { StarRating } from './StarRating';

describe('StarRating', () => {
  it('renders 5 stars, filled up to the current value', () => {
    render(<StarRating value={3} onRate={vi.fn()} onClear={vi.fn()} />);

    expect(screen.getByRole('button', { name: 'Rate 1 star' })).toHaveTextContent('★');
    expect(screen.getByRole('button', { name: 'Rate 2 stars' })).toHaveTextContent('★');
    expect(screen.getByRole('button', { name: 'Rate 3 stars' })).toHaveTextContent('★');
    expect(screen.getByRole('button', { name: 'Rate 4 stars' })).toHaveTextContent('☆');
    expect(screen.getByRole('button', { name: 'Rate 5 stars' })).toHaveTextContent('☆');
  });

  it('renders every star unfilled when there is no rating', () => {
    render(<StarRating value={null} onRate={vi.fn()} onClear={vi.fn()} />);

    for (let star = 1; star <= 5; star++) {
      expect(screen.getByRole('button', { name: `Rate ${star} star${star === 1 ? '' : 's'}` })).toHaveTextContent(
        '☆',
      );
    }
  });

  it('calls onRate with the clicked star value', async () => {
    const onRate = vi.fn();
    const user = userEvent.setup();
    render(<StarRating value={null} onRate={onRate} onClear={vi.fn()} />);

    await user.click(screen.getByRole('button', { name: 'Rate 4 stars' }));

    expect(onRate).toHaveBeenCalledWith(4);
  });

  it('shows the remove-rating button only when a rating exists, and calls onClear', async () => {
    const onClear = vi.fn();
    const user = userEvent.setup();
    const { rerender } = render(<StarRating value={null} onRate={vi.fn()} onClear={onClear} />);

    expect(screen.queryByRole('button', { name: 'Remove rating' })).not.toBeInTheDocument();

    rerender(<StarRating value={3} onRate={vi.fn()} onClear={onClear} />);
    await user.click(screen.getByRole('button', { name: 'Remove rating' }));

    expect(onClear).toHaveBeenCalledTimes(1);
  });

  it('disables every button and shows a spinner when disabled', () => {
    render(<StarRating value={2} onRate={vi.fn()} onClear={vi.fn()} disabled />);

    expect(screen.getByRole('button', { name: 'Rate 1 star' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Remove rating' })).toBeDisabled();
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('shows no spinner when not disabled', () => {
    render(<StarRating value={2} onRate={vi.fn()} onClear={vi.fn()} />);

    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });
});
