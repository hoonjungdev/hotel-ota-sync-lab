using System.Collections.Concurrent;

namespace MockOta.BlueWave.Inventory;

public interface IReservationLog
{
    /// Returns true if this booking ID is new; false if it was already recorded
    /// (idempotent re-delivery).
    bool TryRecord(string bookingId);
}

public sealed class InMemoryReservationLog : IReservationLog
{
    private readonly ConcurrentDictionary<string, byte> _seen = new();

    public bool TryRecord(string bookingId) => _seen.TryAdd(bookingId, 0);
}
