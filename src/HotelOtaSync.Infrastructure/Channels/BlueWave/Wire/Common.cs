using System.Xml.Serialization;

namespace HotelOtaSync.Infrastructure.Channels.BlueWave.Wire;

/// Adapter-side copy of the OpenTravel wire types we serialise/deserialise.
/// Intentionally NOT shared with MockOta.BlueWave: the contract test in
/// tests/HotelOtaSync.Contract is what guarantees the two stay in sync, and
/// drift between them is what that test exists to detect.
internal static class OtaXml
{
    public const string Namespace = "http://www.opentravel.org/OTA/2003/05";
}

public sealed class WireRoomTypes
{
    [XmlElement("RoomType")] public WireRoomType RoomType { get; set; } = new();
}

public sealed class WireRoomType
{
    [XmlAttribute("RoomTypeCode")] public string Code { get; set; } = "";
}

public sealed class WireRatePlans
{
    [XmlElement("RatePlan")] public WireRatePlan RatePlan { get; set; } = new();
}

public sealed class WireRatePlan
{
    [XmlAttribute("RatePlanCode")] public string Code { get; set; } = "";
}

public sealed class WireTotal
{
    [XmlAttribute("AmountAfterTax")] public decimal AmountAfterTax { get; set; }
    [XmlAttribute("CurrencyCode")] public string CurrencyCode { get; set; } = "USD";
}

public sealed class WireErrors
{
    [XmlElement("Error")] public List<WireError> Items { get; set; } = new();
}

public sealed class WireError
{
    [XmlAttribute("Type")] public string Type { get; set; } = "";
    [XmlAttribute("Code")] public string Code { get; set; } = "";
    [XmlText] public string Message { get; set; } = "";
}

public sealed class WireEmpty;
