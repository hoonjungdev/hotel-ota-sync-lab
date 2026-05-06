using Microsoft.AspNetCore.Mvc;
using MockOta.BlueWave.Contracts;
using MockOta.BlueWave.Inventory;

namespace MockOta.BlueWave.Controllers;

[ApiController]
[Route("ota")]
[Consumes("application/xml")]
[Produces("application/xml")]
public sealed class HotelAvailController : ControllerBase
{
    private readonly IInventoryStore _store;

    public HotelAvailController(IInventoryStore store) => _store = store;

    [HttpPost("HotelAvailRQ")]
    public ActionResult<OtaHotelAvailRs> Post([FromBody] OtaHotelAvailRq rq)
    {
        var segment = rq.Segments.FirstOrDefault();
        if (segment is null)
        {
            return new OtaHotelAvailRs
            {
                EchoToken = rq.EchoToken,
                Errors = new Errors { Items = { new Error { Type = "3", Code = "392", Message = "AvailRequestSegments missing" } } }
            };
        }

        var hotel = segment.Criteria.Criterion.Hotel.HotelCode;
        if (!DateOnly.TryParse(segment.Criteria.Criterion.Stay.Start, out var start) ||
            !DateOnly.TryParse(segment.Criteria.Criterion.Stay.End, out var end) ||
            end <= start)
        {
            return new OtaHotelAvailRs
            {
                EchoToken = rq.EchoToken,
                Errors = new Errors { Items = { new Error { Type = "3", Code = "316", Message = "Invalid StayDateRange" } } }
            };
        }

        var stays = _store.Query(hotel, start, end)
            .Select(a => new RoomStay
            {
                StayDate = a.StayDate.ToString("yyyy-MM-dd"),
                Available = a.Available,
                Rates = new RatePlans { RatePlan = new RatePlan { Code = a.RatePlanCode } },
                Rooms = new RoomTypes { RoomType = new RoomType { Code = a.RoomTypeCode } },
                Total = new Total { AmountAfterTax = a.AmountAfterTax, CurrencyCode = a.CurrencyCode }
            })
            .ToList();

        return new OtaHotelAvailRs
        {
            EchoToken = rq.EchoToken,
            Success = new Empty(),
            RoomStays = stays
        };
    }
}
