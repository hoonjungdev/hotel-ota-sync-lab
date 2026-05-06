using System.Xml.Serialization;

namespace MockOta.BlueWave.Contracts;

[XmlRoot("OTA_HotelAvailRQ", Namespace = OtaXml.Namespace)]
public sealed class OtaHotelAvailRq
{
    [XmlAttribute("Version")] public string Version { get; set; } = "1.003";
    [XmlAttribute("EchoToken")] public string? EchoToken { get; set; }

    [XmlArray("AvailRequestSegments")]
    [XmlArrayItem("AvailRequestSegment")]
    public List<AvailRequestSegment> Segments { get; set; } = new();
}

public sealed class AvailRequestSegment
{
    [XmlElement("HotelSearchCriteria")]
    public HotelSearchCriteria Criteria { get; set; } = new();
}

public sealed class HotelSearchCriteria
{
    [XmlElement("Criterion")] public Criterion Criterion { get; set; } = new();
}

public sealed class Criterion
{
    [XmlElement("HotelRef")] public HotelRef Hotel { get; set; } = new();
    [XmlElement("StayDateRange")] public StayDateRange Stay { get; set; } = new();
}

public sealed class HotelRef
{
    [XmlAttribute("HotelCode")] public string HotelCode { get; set; } = "";
}

public sealed class StayDateRange
{
    [XmlAttribute("Start")] public string Start { get; set; } = "";
    [XmlAttribute("End")] public string End { get; set; } = "";
}

[XmlRoot("OTA_HotelAvailRS", Namespace = OtaXml.Namespace)]
public sealed class OtaHotelAvailRs
{
    [XmlAttribute("Version")] public string Version { get; set; } = "1.003";
    [XmlAttribute("EchoToken")] public string? EchoToken { get; set; }
    [XmlAttribute("TimeStamp")] public string TimeStamp { get; set; } = DateTimeOffset.UtcNow.ToString("o");

    [XmlElement("Success")] public Empty? Success { get; set; }
    [XmlElement("Errors")] public Errors? Errors { get; set; }

    [XmlArray("RoomStays")]
    [XmlArrayItem("RoomStay")]
    public List<RoomStay> RoomStays { get; set; } = new();
}

public sealed class RoomStay
{
    [XmlElement("RatePlans")] public RatePlans Rates { get; set; } = new();
    [XmlElement("RoomTypes")] public RoomTypes Rooms { get; set; } = new();
    [XmlElement("StayDate")] public string StayDate { get; set; } = "";
    [XmlElement("Total")] public Total Total { get; set; } = new();
    [XmlElement("Available")] public int Available { get; set; }
}

public sealed class RatePlans
{
    [XmlElement("RatePlan")] public RatePlan RatePlan { get; set; } = new();
}

public sealed class RatePlan
{
    [XmlAttribute("RatePlanCode")] public string Code { get; set; } = "";
}

public sealed class RoomTypes
{
    [XmlElement("RoomType")] public RoomType RoomType { get; set; } = new();
}

public sealed class RoomType
{
    [XmlAttribute("RoomTypeCode")] public string Code { get; set; } = "";
}

public sealed class Total
{
    [XmlAttribute("AmountAfterTax")] public decimal AmountAfterTax { get; set; }
    [XmlAttribute("CurrencyCode")] public string CurrencyCode { get; set; } = "USD";
}

public sealed class Errors
{
    [XmlElement("Error")] public List<Error> Items { get; set; } = new();
}

public sealed class Error
{
    [XmlAttribute("Type")] public string Type { get; set; } = "";
    [XmlAttribute("Code")] public string Code { get; set; } = "";
    [XmlText] public string Message { get; set; } = "";
}

public sealed class Empty;
