namespace HotelOtaSync.Application.Channels;

/// Stable identifier for an OTA partner. The string value travels into logs,
/// metrics, and the reservation table's unique key, so it must never change
/// once a channel ships.
public readonly record struct ChannelCode(string Value)
{
    public static readonly ChannelCode BlueWave = new("BlueWave");
    public static readonly ChannelCode SkyTrip = new("SkyTrip");
    public static readonly ChannelCode GreenLeaf = new("GreenLeaf");

    public override string ToString() => Value;
}
