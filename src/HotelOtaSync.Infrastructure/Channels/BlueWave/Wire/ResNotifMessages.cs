using System.Xml.Serialization;

namespace HotelOtaSync.Infrastructure.Channels.BlueWave.Wire;

[XmlRoot("OTA_HotelResNotifRQ", Namespace = OtaXml.Namespace)]
public sealed class WireResNotifRq
{
    [XmlAttribute("Version")] public string Version { get; set; } = "1.003";
    [XmlAttribute("EchoToken")] public string? EchoToken { get; set; }
    [XmlAttribute("ResStatus")] public string ResStatus { get; set; } = "Commit";

    [XmlArray("HotelReservations")]
    [XmlArrayItem("HotelReservation")]
    public List<WireHotelReservation> Reservations { get; set; } = new();
}

public sealed class WireHotelReservation
{
    [XmlAttribute("CreateDateTime")] public string CreateDateTime { get; set; } = DateTimeOffset.UtcNow.ToString("o");
    [XmlAttribute("ResStatus")] public string ResStatus { get; set; } = "Confirmed";

    [XmlArray("UniqueID")]
    [XmlArrayItem("UniqueID")]
    public List<WireUniqueId> UniqueIds { get; set; } = new();

    [XmlElement("RoomStays")] public WireResRoomStays RoomStays { get; set; } = new();
    [XmlElement("ResGuests")] public WireResGuests Guests { get; set; } = new();
    [XmlElement("ResGlobalInfo")] public WireResGlobalInfo GlobalInfo { get; set; } = new();
}

public sealed class WireUniqueId
{
    [XmlAttribute("Type")] public string Type { get; set; } = "14";
    [XmlAttribute("ID")] public string Id { get; set; } = "";
    [XmlAttribute("ID_Context")] public string? Context { get; set; }
}

public sealed class WireResRoomStays
{
    [XmlElement("RoomStay")] public WireResRoomStay RoomStay { get; set; } = new();
}

public sealed class WireResRoomStay
{
    [XmlElement("RoomTypes")] public WireRoomTypes Rooms { get; set; } = new();
    [XmlElement("RatePlans")] public WireRatePlans Rates { get; set; } = new();
    [XmlElement("GuestCounts")] public WireGuestCounts GuestCounts { get; set; } = new();
    [XmlElement("TimeSpan")] public WireTimeSpanRange TimeSpan { get; set; } = new();
    [XmlElement("Total")] public WireTotal Total { get; set; } = new();
}

public sealed class WireGuestCounts
{
    [XmlElement("GuestCount")] public WireGuestCount Count { get; set; } = new();
}

public sealed class WireGuestCount
{
    [XmlAttribute("AgeQualifyingCode")] public string AgeQualifying { get; set; } = "10";
    [XmlAttribute("Count")] public int Count { get; set; }
}

public sealed class WireTimeSpanRange
{
    [XmlAttribute("Start")] public string Start { get; set; } = "";
    [XmlAttribute("End")] public string End { get; set; } = "";
}

public sealed class WireResGuests
{
    [XmlElement("ResGuest")] public WireResGuest Guest { get; set; } = new();
}

public sealed class WireResGuest
{
    [XmlElement("Profiles")] public WireProfiles Profiles { get; set; } = new();
}

public sealed class WireProfiles
{
    [XmlElement("ProfileInfo")] public WireProfileInfo Profile { get; set; } = new();
}

public sealed class WireProfileInfo
{
    [XmlElement("Profile")] public WireProfile Inner { get; set; } = new();
}

public sealed class WireProfile
{
    [XmlElement("Customer")] public WireCustomer Customer { get; set; } = new();
}

public sealed class WireCustomer
{
    [XmlElement("PersonName")] public WirePersonName Name { get; set; } = new();
}

public sealed class WirePersonName
{
    [XmlElement("GivenName")] public string GivenName { get; set; } = "";
    [XmlElement("Surname")] public string Surname { get; set; } = "";
}

public sealed class WireResGlobalInfo
{
    [XmlElement("HotelReservationIDs")] public WireHotelReservationIds Ids { get; set; } = new();
}

public sealed class WireHotelReservationIds
{
    [XmlElement("HotelReservationID")] public List<WireHotelReservationId> Items { get; set; } = new();
}

public sealed class WireHotelReservationId
{
    [XmlAttribute("ResID_Type")] public string Type { get; set; } = "14";
    [XmlAttribute("ResID_Value")] public string Value { get; set; } = "";
    [XmlAttribute("ResID_Source")] public string? Source { get; set; }
}

[XmlRoot("OTA_HotelResNotifRS", Namespace = OtaXml.Namespace)]
public sealed class WireResNotifRs
{
    [XmlAttribute("Version")] public string Version { get; set; } = "1.003";
    [XmlAttribute("EchoToken")] public string? EchoToken { get; set; }
    [XmlAttribute("TimeStamp")] public string? TimeStamp { get; set; }

    [XmlElement("Success")] public WireEmpty? Success { get; set; }
    [XmlElement("Errors")] public WireErrors? Errors { get; set; }

    [XmlArray("HotelReservations")]
    [XmlArrayItem("HotelReservation")]
    public List<WireHotelReservationAck> Acks { get; set; } = new();
}

public sealed class WireHotelReservationAck
{
    [XmlElement("ResGlobalInfo")] public WireResGlobalInfo GlobalInfo { get; set; } = new();
}
