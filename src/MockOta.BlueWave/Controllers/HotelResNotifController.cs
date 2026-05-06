using Microsoft.AspNetCore.Mvc;
using MockOta.BlueWave.Contracts;
using MockOta.BlueWave.Inventory;

namespace MockOta.BlueWave.Controllers;

[ApiController]
[Route("ota")]
[Consumes("application/xml")]
[Produces("application/xml")]
public sealed class HotelResNotifController : ControllerBase
{
    private readonly IReservationLog _log;

    public HotelResNotifController(IReservationLog log) => _log = log;

    [HttpPost("HotelResNotifRQ")]
    public ActionResult<OtaHotelResNotifRs> Post([FromBody] OtaHotelResNotifRq rq)
    {
        var acks = new List<HotelReservationAck>();
        foreach (var res in rq.Reservations)
        {
            var bookingId = res.UniqueIds.FirstOrDefault()?.Id ?? Guid.NewGuid().ToString("N");
            _log.TryRecord(bookingId);

            acks.Add(new HotelReservationAck
            {
                GlobalInfo = new ResGlobalInfo
                {
                    Ids = new HotelReservationIds
                    {
                        Items =
                        {
                            new HotelReservationId { Type = "14", Value = bookingId, Source = "BlueWave" }
                        }
                    }
                }
            });
        }

        return new OtaHotelResNotifRs
        {
            EchoToken = rq.EchoToken,
            Success = new Empty(),
            Acks = acks
        };
    }
}
