using System.ComponentModel.DataAnnotations;

namespace HotelOtaSync.Api.Auth;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    /// Bearer token required by every /admin/* endpoint. Bound from the
    /// "Admin:Token" configuration key. Validated on startup; see
    /// AdminOptionsValidator for the production-environment guard.
    [Required, MinLength(16)]
    public string Token { get; init; } = "";
}
