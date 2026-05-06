namespace MockOta.BlueWave.Inventory;

/// Deterministic synthetic ARI: price varies by weekday, availability decays
/// across the booking window. Seeded so the same date returns the same value.
public sealed class InMemoryInventoryStore : IInventoryStore
{
    private static readonly string[] RoomTypes = ["DLX", "STD"];
    private static readonly string[] RatePlans = ["BAR", "FLEX"];

    public IEnumerable<DailyAri> Query(string hotelCode, DateOnly start, DateOnly endExclusive)
    {
        for (var d = start; d < endExclusive; d = d.AddDays(1))
        {
            foreach (var room in RoomTypes)
            foreach (var plan in RatePlans)
            {
                var seed = HashCode.Combine(hotelCode, room, plan, d.DayNumber);
                var weekendBump = d.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday ? 1.25m : 1.0m;
                var basePrice = room == "DLX" ? 220m : 140m;
                var planAdj = plan == "FLEX" ? 1.15m : 1.0m;
                var price = Math.Round(basePrice * weekendBump * planAdj, 2);
                var avail = Math.Max(0, 8 - Math.Abs(seed % 5));

                yield return new DailyAri(hotelCode, room, plan, d, avail, price, "USD");
            }
        }
    }
}
