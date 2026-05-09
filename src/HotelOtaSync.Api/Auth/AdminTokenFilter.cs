using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace HotelOtaSync.Api.Auth;

/// IEndpointFilter attached to the /admin route group. Validates the
/// Authorization: Bearer header against AdminOptions.Token using a constant-
/// time compare so timing analysis can't shrink the search space.
public sealed class AdminTokenFilter(IOptions<AdminOptions> options) : IEndpointFilter
{
    private readonly byte[] _expected = Encoding.UTF8.GetBytes(options.Value.Token);

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var header = context.HttpContext.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";

        if (!header.StartsWith(prefix, StringComparison.Ordinal))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Missing or malformed Authorization header.");
        }

        var presented = Encoding.UTF8.GetBytes(header[prefix.Length..]);

        if (presented.Length != _expected.Length ||
            !CryptographicOperations.FixedTimeEquals(presented, _expected))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid admin token.");
        }

        return await next(context);
    }
}
