namespace HotelOtaSync.Domain;

/// Amount + ISO-4217 currency code. Equality is structural so ratepoints can
/// be compared in tests without a custom comparer.
public readonly record struct Money(decimal Amount, string Currency)
{
    public override string ToString() => $"{Amount:0.00} {Currency}";
}
