namespace HotelOtaSync.Domain;

/// Hotel-domain date range, half-open: [Start, End). Length = nights stayed.
/// Mirrors OpenTravel StayDateRange so adapters map 1:1 without arithmetic.
public readonly record struct DateRange
{
    public DateOnly Start { get; }
    public DateOnly End { get; }

    public DateRange(DateOnly start, DateOnly end)
    {
        if (end <= start)
            throw new ArgumentException($"DateRange end ({end}) must be after start ({start}).", nameof(end));
        Start = start;
        End = end;
    }

    public int Nights => End.DayNumber - Start.DayNumber;

    public IEnumerable<DateOnly> EachStayDate()
    {
        for (var d = Start; d < End; d = d.AddDays(1)) yield return d;
    }
}
