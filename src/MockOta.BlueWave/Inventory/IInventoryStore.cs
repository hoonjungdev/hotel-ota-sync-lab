namespace MockOta.BlueWave.Inventory;

public interface IInventoryStore
{
    IEnumerable<DailyAri> Query(string hotelCode, DateOnly start, DateOnly endExclusive);
}

public sealed record DailyAri(
    string HotelCode,
    string RoomTypeCode,
    string RatePlanCode,
    DateOnly StayDate,
    int Available,
    decimal AmountAfterTax,
    string CurrencyCode);
