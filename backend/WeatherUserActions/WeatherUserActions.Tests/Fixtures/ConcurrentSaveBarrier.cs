using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace WeatherUserActions.Tests.Fixtures
{
    // Pauses each participating DbContext right before its FIRST SaveChangesAsync command,
    // releasing them all only once every participant has arrived - so a test asserting on a
    // concurrent-write race (e.g. a unique-index conflict) doesn't depend on two independent
    // DbContexts happening to overlap by chance; Task.WhenAll alone doesn't guarantee that.
    //
    // Only the first save per DbContext is gated. A repository method that issues several
    // SaveChangesAsync calls inside one open transaction (e.g. SelectionsRepository) can have
    // its later calls land at very different times once a real DB-level lock wait kicks in for
    // the loser of the race - gating every call would then deadlock the two contexts waiting on
    // each other. Gating only the first call still forces the one collision a test cares about,
    // and leaves the rest of each transaction free to resolve on its own.
    public sealed class ConcurrentSaveBarrier : SaveChangesInterceptor
    {
        private readonly Barrier _barrier;
        private readonly HashSet<DbContext> _alreadySynced = [];
        private readonly Lock _lock = new();

        public ConcurrentSaveBarrier(int participantCount)
        {
            _barrier = new Barrier(participantCount);
        }

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
}
