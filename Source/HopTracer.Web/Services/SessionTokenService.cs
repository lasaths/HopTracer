using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace HopTracer.Web.Services;

public interface ISessionTokenService
{
    string Token { get; }
    bool IsValid(string? token);
}

public sealed class SessionTokenService : ISessionTokenService
{
    public string Token { get; } = CreateToken();

    public bool IsValid(string? token)
    {
        return !string.IsNullOrWhiteSpace(token) &&
               string.Equals(token, Token, StringComparison.Ordinal);
    }

    private static string CreateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }
}

public sealed class SessionTokenMiddleware
{
    private const string TokenHeaderName = "X-HopTracer-Session";
    private const string TokenFormFieldName = "_session_token";

    private static readonly string[] ProtectedPrefixes =
    [
        "/git/",
        "/review/",
        "/api/filepicker/"
    ];

    private static readonly string[] ProtectedExactPaths =
    [
        "/compare",
        "/system/pick_file"
    ];

    private readonly RequestDelegate _next;
    private readonly ILogger<SessionTokenMiddleware> _logger;

    public SessionTokenMiddleware(RequestDelegate next, ILogger<SessionTokenMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ISessionTokenService tokenService)
    {
        if (!RequiresToken(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var providedToken = GetHeaderToken(context.Request);
        if (string.IsNullOrWhiteSpace(providedToken) && context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            providedToken = form[TokenFormFieldName].FirstOrDefault();
        }

        if (!tokenService.IsValid(providedToken))
        {
            _logger.LogWarning("Rejected request due to invalid session token. Path: {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid session token." }, context.RequestAborted);
            return;
        }

        await _next(context);
    }

    private static bool RequiresToken(PathString path)
    {
        var value = path.Value ?? string.Empty;
        if (ProtectedExactPaths.Any(p => string.Equals(value, p, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return ProtectedPrefixes.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetHeaderToken(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(TokenHeaderName, out var tokenValues))
        {
            return null;
        }

        return tokenValues.FirstOrDefault();
    }
}
