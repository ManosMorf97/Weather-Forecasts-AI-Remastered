export function LoadingScreen() {
  return (
    <div className="d-flex justify-content-center align-items-center flex-grow-1">
      <div className="spinner-border" role="status" style={{ color: 'var(--accent)' }}>
        <span className="visually-hidden">Loading…</span>
      </div>
    </div>
  );
}
