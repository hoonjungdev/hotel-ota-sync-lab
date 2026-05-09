using Microsoft.Extensions.Options;

namespace HotelOtaSync.Api.Auth;

/// Defense-in-depth boot-time guard: refuses startup when the host environment
/// is not Development AND the configured admin token still contains the
/// "INSECURE-DEV-ONLY" sentinel. Pairs with the loud-bad default in
/// docker-compose.yml so anyone shipping this past dev has to override the
/// token explicitly — the dev/prod gap is enforced in code, not just prose.
public sealed class AdminOptionsValidator(IHostEnvironment env) : IValidateOptions<AdminOptions>
{
    internal const string DevSentinel = "INSECURE-DEV-ONLY";

    public ValidateOptionsResult Validate(string? name, AdminOptions options)
    {
        if (!env.IsDevelopment() && options.Token.Contains(DevSentinel, StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail(
                $"Refusing to start in '{env.EnvironmentName}' environment with the dev " +
                $"admin token (contains '{DevSentinel}'). Set Admin:Token (e.g. via the " +
                "ADMIN_TOKEN env var) to a real rotated value.");
        }
        return ValidateOptionsResult.Success;
    }
}
