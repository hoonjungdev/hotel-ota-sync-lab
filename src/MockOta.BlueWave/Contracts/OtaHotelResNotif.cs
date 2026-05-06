using System.Xml.Serialization;

namespace MockOta.BlueWave.Contracts;

[XmlRoot("OTA_HotelResNotifRQ", Namespace = OtaXml.Namespace)]
public sealed class OtaHotelResNotifRq
{
    [XmlAttribute("Version")] public string Version { get; set; } = "1.003";
    [XmlAttribute("EchoToken")] public string? EchoToken { get; set; }
    [XmlAttribute("ResStatus")] public string ResStatus { get; set; } = "Commit";

    [XmlArray("HotelReservations")]
    [XmlArrayItem("HotelReservation")]
    public List<HotelReservation> Reservations { get; set; } = new();
}

public sealed class HotelReservation
{
    [XmlAttribute("CreateDateTime")] public string CreateDateTime { get; set; } = DateTimeOffset.UtcNow.ToString("o");
    [XmlAttribute("ResStatus")] public string ResStatus { get; set; } = "Confirmed";

    [XmlArray("UniqueID")]
    [XmlArrayItem("UniqueID")]
    public List<UniqueId> UniqueIds { get; set; } = new();

    [XmlElement("RoomStays")] public ResRoomStays RoomStays { get; set; } = new();
    [XmlElement("ResGuests")] public ResGuests Guests { get; set; } = new();
    [XmlElement("ResGlobalInfo")] public ResGlobalInfo GlobalInfo { get; set; } = new();
}

public sealed class UniqueId
{
    [XmlAttribute("Type")] public string Type { get; set; } = "14";  // 14 = reservation
    [XmlAttribute("ID")] public string Id { get; set; } = "";
    [XmlAttribute("ID_Context")] public string? Context { get; set; }
}

public sealed class ResRoomStays
{
    [XmlElement("RoomStay")] public ResRoomStay RoomStay { get; set; } = new();
}

public sealed class ResRoomStay
{
    [XmlElement("RoomTypes")] public RoomTypes Rooms { get; set; } = new();
    [XmlElement("RatePlans")] public RatePlans Rates { get; set; } = new();
    [XmlElement("GuestCounts")] public GuestCounts GuestCounts { get; set; } = new();
    [XmlElement("TimeSpan")] public TimeSpanRange TimeSpan { get; set; } = new();
    [XmlElement("Total")] public Total Total { get; set; } = new();
}

public sealed class GuestCounts
{
    [XmlElement("GuestCount")] public GuestCount Count { get; set; } = new();
}

public sealed class GuestCount
{
    [XmlAttribute("AgeQualifyingCode")] public string AgeQualifying { get; set; } = "10"; // 10 = adult
    [XmlAttribute("Count")] public int Count { get; set; }
}

public sealed class TimeSpanRange
{
    [XmlAttribute("Start")] public string Start { get; set; } = "";
    [XmlAttribute("End")] public string End { get; set; } = "";
}

public sealed class ResGuests
{
    [XmlElement("ResGuest")] public ResGuest Guest { get; set; } = new();
}

public sealed class ResGuest
{
    [XmlElement("Profiles")] public Profiles Profiles { get; set; } = new();
}

public sealed class Profiles
{
    [XmlElement("ProfileInfo")] public ProfileInfo Profile { get; set; } = new();
}

public sealed class ProfileInfo
{
    [XmlElement("Profile")] public Profile Inner { get; set; } = new();
}

public sealed class Profile
{
    [XmlElement("Customer")] public Customer Customer { get; set; } = new();
}

public sealed class Customer
{
    [XmlElement("PersonName")] public PersonName Name { get; set; } = new();
}

public sealed class PersonName
{
    [XmlElement("GivenName")] public string GivenName { get; set; } = "";
    [XmlElement("Surname")] public string Surname { get; set; } = "";
}

public sealed class ResGlobalInfo
{
    [XmlElement("HotelReservationIDs")] public HotelReservationIds Ids { get; set; } = new();
}

public sealed class HotelReservationIds
{
    [XmlElement("HotelReservationID")] public List<HotelReservationId> Items { get; set; } = new();
}

public sealed class HotelReservationId
{
    [XmlAttribute("ResID_Type")] public string Type { get; set; } = "14";
    [XmlAttribute("ResID_Value")] public string Value { get; set; } = "";
    [XmlAttribute("ResID_Source")] public string? Source { get; set; }
}

[XmlRoot("OTA_HotelResNotifRS", Namespace = OtaXml.Namespace)]
public sealed class OtaHotelResNotifRs
{
    [XmlAttribute("Version")] public string Version { get; set; } = "1.003";
    [XmlAttribute("EchoToken")] public string? EchoToken { get; set; }
    [XmlAttribute("TimeStamp")] public string TimeStamp { get; set; } = DateTimeOffset.UtcNow.ToString("o");

    [XmlElement("Success")] public Empty? Success { get; set; }
    [XmlElement("Errors")] public Errors? Errors { get; set; }

    [XmlArray("HotelReservations")]
    [XmlArrayItem("HotelReservation")]
    public List<HotelReservationAck> Acks { get; set; } = new();
}

public sealed class HotelReservationAck
{
    [XmlElement("ResGlobalInfo")] public ResGlobalInfo GlobalInfo { get; set; } = new();
}
