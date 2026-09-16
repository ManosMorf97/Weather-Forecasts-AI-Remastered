# Testing Concurrency — Learning Notes

Notes on how to (and how not to) test "what happens when two requests hit the
same row at once" against a real database, based on fixing a flaky/deadlocking
test in `RatingsRepositoryTests.cs`, `ProfileRepositoryTests.cs`, and
`SelectionsRepositoryTests.cs`.

Files involved:

| File | Role |
|------|------|
| `WeatherUserActions.Tests/Fixtures/ConcurrentSaveBarrier.cs` | The fix: forces two `DbContext`s to race for real |
| `WeatherUserActions.Tests/Fixtures/SqlServerFixture.cs` | `CreateDbContext(...)` accepts it as an optional interceptor |
| `WeatherUserActions.Tests/RatingsRepositoryTests.cs` | Uses it — single-save repository |
| `WeatherUserActions.Tests/ProfileRepositoryTests.cs` | Uses it — single-save repository |
| `WeatherUserActions.Tests/SelectionsRepositoryTests.cs` | Uses it — multi-save repository (the tricky case) |

---

## 1. What we're trying to test

Several repositories do "check if a row exists, then insert it" — and rely on
a **unique index** to catch the case where two requests do that check at the
same instant and both decide to insert. One of two things should happen:

- The loser's insert fails outright (`RatingsRepository` — no retry, caller
  sees `Succeeded: false`), or
- The loser catches that failure, re-checks, and treats "someone else already
  inserted it" as success (`ProfileRepository`, `SelectionsRepository`).

A test for this needs to **actually make two inserts collide** — not just
hope they do.

---

## 2. Wrong: bare `Task.WhenAll`

```csharp
// ❌ Wrong — no guarantee these two calls actually overlap
var results = await Task.WhenAll(
    repositoryA.UpsertRatingAsync(uid, forecastId, value: 3),
    repositoryB.UpsertRatingAsync(uid, forecastId, value: 4));

Assert.Contains(results, r => r.Succeeded);
Assert.Contains(results, r => !r.Succeeded);  // flaky: sometimes NEITHER fails
```

`Task.WhenAll` starts both tasks, but nothing forces their database
round-trips to line up. If repository A's whole sequence — check, insert,
commit — finishes before repository B's check even runs, B just sees A's row
and takes the "update" path instead of racing on insert. No conflict, no
failure, and `Assert.Contains(results, r => !r.Succeeded)` fails — not
because the code is wrong, but because the two calls never actually collided
this run. **Flaky**: passes most of the time locally, fails occasionally,
especially on a loaded CI runner.

---

## 3. Wrong: synchronizing *every* save

The fix is to force the two contexts to reach their `SaveChangesAsync` at the
same instant with a `Barrier`. First attempt:

```csharp
// ❌ Wrong — synchronizes on EVERY SaveChangesAsync call, not just the first
public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
    DbContextEventData eventData,
    InterceptionResult<int> result,
    CancellationToken cancellationToken = default)
{
    await Task.Run(() => _barrier.SignalAndWait(cancellationToken), cancellationToken);
    return await base.SavingChangesAsync(eventData, result, cancellationToken);
}
```

This works for a repository that calls `SaveChangesAsync` exactly once per
method call (`RatingsRepository`, `ProfileRepository`). It **deadlocks** for
one that calls it several times inside one open transaction
(`SelectionsRepository.ReplaceUserSelectionAsync` — one save for cities, one
for city-sites, one final save, all before `CommitAsync`):

```csharp
// SelectionsRepository.ReplaceUserSelectionAsync (simplified)
await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
var cityIds = await UpsertCitiesAsync(cities, cancellationToken);        // save #1
var citySiteIds = await UpsertCitySitesAsync(cityIds, ...);              // save #2
await ReplaceUserCitySitesAsync(userId, citySiteIds, cancellationToken);
await ClearPendingUserServicesAsync(userId, cancellationToken);
await _db.SaveChangesAsync(cancellationToken);                           // save #3
await transaction.CommitAsync(cancellationToken);                        // only now released
```

Walkthrough of the deadlock:

1. A and B both reach save #1 together (barrier releases them).
2. A's insert succeeds immediately. B's insert **blocks for real** — SQL
   Server makes a concurrent insert into the same unique key wait for A's
   transaction to commit or roll back, since A hasn't reached `CommitAsync`
   yet. B is now stuck *inside* the database call, nowhere near the barrier.
3. A moves on, reaches save #2, and calls the barrier again — waiting for B.
4. B never arrives, because B is still blocked back at save #1.
5. Neither side ever unblocks. The test hangs (confirmed — it ran past 90
   seconds with no result before it was manually killed).

---

## 4. Right: synchronize only the *first* save per context

```csharp
// ✅ Right — only the first SaveChangesAsync per DbContext is gated
public sealed class ConcurrentSaveBarrier : SaveChangesInterceptor
{
    private readonly Barrier _barrier;
    private readonly HashSet<DbContext> _alreadySynced = [];
    private readonly Lock _lock = new();

    public ConcurrentSaveBarrier(int participantCount) => _barrier = new Barrier(participantCount);

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var isFirstSaveForThisContext = false;
        if (eventData.Context is not null)
        {
            lock (_lock)
            {
                isFirstSaveForThisContext = _alreadySynced.Add(eventData.Context);
            }
        }

        if (isFirstSaveForThisContext)
        {
            await Task.Run(() => _barrier.SignalAndWait(cancellationToken), cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
```

Same walkthrough, fixed:

1. A and B both reach save #1 together (barrier releases them) — this is the
   one collision the test actually cares about.
2. A's insert succeeds, B's blocks on the real DB lock — same as before.
3. A reaches save #2 and #3 — **no barrier this time**, so it just runs them
   and calls `CommitAsync`, releasing its lock.
4. B's blocked insert (from step 2) now unblocks, sees the conflict, and its
   own existing `catch (DbUpdateException)` recovery logic kicks in — the
   thing the test was meant to exercise in the first place.
5. B finishes normally. Both `Task`s complete. No deadlock.

Verified by actually running it — not just reasoning about it — 5 times back
to back with no hang, plus the full 150-test suite green.

---

## 5. How to use it

```csharp
var barrier = new ConcurrentSaveBarrier(participantCount: 2);
await using var dbA = _fixture.CreateDbContext(barrier);
await using var dbB = _fixture.CreateDbContext(barrier);
var repositoryA = new SomeRepository(dbA, NullLogger<SomeRepository>.Instance);
var repositoryB = new SomeRepository(dbB, NullLogger<SomeRepository>.Instance);

var results = await Task.WhenAll(
    repositoryA.SomeUpsertAsync(...),
    repositoryB.SomeUpsertAsync(...));
```

One `ConcurrentSaveBarrier` instance is shared between both `DbContext`s
(that's what makes them wait for *each other*, not two separate barriers each
waiting alone). `participantCount` should match how many contexts you're
racing — 2 for a two-way race.

---

## 6. Takeaways

- A `Task.WhenAll` of two DB calls is **not** a race unless something forces
  their timing to overlap — don't assert on race outcomes without one.
- When you do force a race, only synchronize the **one operation you're
  actually testing** — not every database round-trip a method happens to
  make. Over-synchronizing can deadlock against a real database lock in a way
  that never shows up as a normal, readable test failure — it just hangs.
- When something might hang, **run it and watch**, don't just reason about
  it — the reasoning here was right, but it was only *confirmed* right by
  actually killing a hung process and re-testing.
