using System.Xml.Serialization;

namespace HotelOtaSync.Infrastructure.Channels.BlueWave.Wire;

[XmlRoot("OTA_HotelAvailRQ", Namespace = OtaXml.Namespace)]
public sealed class WireAvailRq
{
    [XmlAttribute("Version")] public string Version { get; set; } = "1.003";
    [XmlAttribute("EchoToken")] public string? EchoToken { get; set; }

    [XmlArray("AvailRequestSegments")]
    [XmlArrayItem("AvailRequestSegment")]
    public List<WireAvailSegment> Segments { get; set; } = new();
}

public sealed class WireAvailSegment
{
    [XmlElement("HotelSearchCriteria")]
    public WireSearchCriteria Criteria { get; set; } = new();
}

public sealed class WireSearchCriteria
{
    [XmlElement("Criterion")] public WireCriterion Criterion { get; set; } = new();
}

public sealed class WireCriterion
{
    [XmlElement("HotelRef")] public WireHotelRef Hotel { get; set; } = new();
    [XmlElement("StayDateRange")] public WireStayDateRange Stay { get; set; } = new();
}

public sealed class WireHotelRef
{
    [XmlAttribute("HotelCode")] public string HotelCode { get; set; } = "";
}

public sealed class WireStayDateRange
{
    [XmlAttribute("Start")] public string Start { get; set; } = "";
    [XmlAttribute("End")] public string End { get; set; } = "";
}

[XmlRoot("OTA_HotelAvailRS", Namespace = OtaXml.Namespace)]
public sealed class WireAvailRs
{
    [XmlAttribute("Version")] public string Version { get; set; } = "1.003";
    [XmlAttribute("EchoToken")] public string? EchoToken { get; set; }
    [XmlAttribute("TimeStamp")] public string? TimeStamp { get; set; }

    [XmlElement("Success")] public WireEmpty? Success { get; set; }
    [XmlElement("Errors")] public WireErrors? Errors { get; set; }

    [XmlArray("RoomStays")]
    [XmlArrayItem("RoomStay")]
    public List<WireRoomStay> RoomStays { get; set; } = new();
}

public sealed class WireRoomStay
{
    [XmlElement("RatePlans")] public WireRatePlans Rates { get; set; } = new();
    [XmlElement("RoomTypes")] public WireRoomTypes Rooms { get; set; } = new();
    [XmlElement("StayDate")] public string StayDate { get; set; } = "";
    [XmlElement("Total")] public WireTotal Total { get; set; } = new();
    [XmlElement("Available")] public int Available { get; set; }
}
