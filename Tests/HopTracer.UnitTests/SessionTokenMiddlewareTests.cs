using System.Text;
using HopTracer.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HopTracer.UnitTests;

public class SessionTokenMiddlewareTests
{
    [Fact]
    public async Task ProtectedPath_MissingToken_ReturnsUnauthorized()
    {
        var nextCalled = false;
        var middleware = new SessionTokenMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<SessionTokenMiddleware>.Instance);

        var context = CreateContext("/git/check", method: "POST");
        var tokenService = new SessionTokenService();

        await middleware.InvokeAsync(context, tokenService);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task ProtectedPath_ValidHeaderToken_AllowsRequest()
    {
        var nextCalled = false;
        var middleware = new SessionTokenMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<SessionTokenMiddleware>.Instance);

        var tokenService = new SessionTokenService();
        var context = CreateContext("/review/baseline/save", method: "POST");
        context.Request.Headers["X-HopTracer-Session"] = tokenService.Token;

        await middleware.InvokeAsync(context, tokenService);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task ComparePath_ValidFormToken_AllowsRequest()
    {
        var nextCalled = false;
        var middleware = new SessionTokenMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<SessionTokenMiddleware>.Instance);

        var tokenService = new SessionTokenService();
        var context = CreateContext("/compare", method: "POST");
        SetUrlEncodedFormBody(context, $"_session_token={Uri.EscapeDataString(tokenService.Token)}");

        await middleware.InvokeAsync(context, tokenService);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task ProtectedPath_InvalidToken_ReturnsUnauthorized()
    {
        var nextCalled = false;
        var middleware = new SessionTokenMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<SessionTokenMiddleware>.Instance);

        var tokenService = new SessionTokenService();
        var context = CreateContext("/system/pick_file", method: "POST");
        context.Request.Headers["X-HopTracer-Session"] = "invalid";

        await middleware.InvokeAsync(context, tokenService);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task SessionTokenEndpoint_IsNotProtected()
    {
        var nextCalled = false;
        var middleware = new SessionTokenMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<SessionTokenMiddleware>.Instance);

        var context = CreateContext("/system/session_token", method: "GET");
        var tokenService = new SessionTokenService();

        await middleware.InvokeAsync(context, tokenService);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    private static DefaultHttpContext CreateContext(string path, string method)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static void SetUrlEncodedFormBody(HttpContext context, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.ContentLength = bytes.Length;
        context.Request.Body = new MemoryStream(bytes);
    }
}
