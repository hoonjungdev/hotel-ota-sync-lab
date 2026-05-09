using HotelOtaSync.Api.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace HotelOtaSync.UnitTests.Auth;

public class AdminTokenFilterTests
{
    private const string Token = "real-rotated-token-please";

    private static AdminTokenFilter Filter() =>
        new(Options.Create(new AdminOptions { Token = Token }));

    private static EndpointFilterInvocationContext ContextWith(string? authHeader)
    {
        var http = new DefaultHttpContext();
        if (authHeader is not null) http.Request.Headers.Authorization = authHeader;
        return new DefaultEndpointFilterInvocationContext(http);
    }

    [Fact]
    public async Task Invoke_ValidBearer_CallsNext()
    {
        var nextCalled = false;
        var result = await Filter().InvokeAsync(
            ContextWith($"Bearer {Token}"),
            _ => { nextCalled = true; return ValueTask.FromResult<object?>(Results.Ok()); });

        Assert.True(nextCalled);
        Assert.IsAssignableFrom<IResult>(result);
    }

    [Fact]
    public async Task Invoke_MissingHeader_Returns401()
    {
        var result = await Filter().InvokeAsync(
            ContextWith(null),
            _ => throw new InvalidOperationException("next must not be called"));

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
    }

    [Fact]
    public async Task Invoke_MismatchedToken_Returns401()
    {
        var result = await Filter().InvokeAsync(
            ContextWith("Bearer wrong-token-value-here"),
            _ => throw new InvalidOperationException("next must not be called"));

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
    }
}
