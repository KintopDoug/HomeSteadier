using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Homesteadier.API.Middleware;

/// <summary>
/// Logs every request (endpoint, authenticated user id, JSON payload) and, on an unhandled
/// exception, the error before rethrowing so the normal error pipeline still runs. Uses
/// structured fields so they flow through the OpenTelemetry logging pipeline configured in
/// HomeSteadier.ServiceDefaults/Extensions.cs rather than a single formatted string.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private const int MaxLoggedBodyBytes = 32 * 1024;

    /// <summary>Substrings that mark a field name as sensitive, wherever it appears in the payload.</summary>
    private static readonly string[] RedactedFieldNameParts = ["password", "token"];

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = $"{context.Request.Method} {context.Request.Path}{context.Request.QueryString}";
        var userId = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? "anonymous";
        var payload = await ReadPayloadAsync(context.Request);

        _logger.LogInformation(
            "Request {Endpoint} by user {UserId} with payload {Payload}",
            endpoint, userId, payload);

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // Passing the exception (rather than just its Message/StackTrace as extra template
            // fields) is what makes the logging providers - console and the OpenTelemetry
            // exporter alike - record the full exception, including stack trace, as structured
            // data rather than flattening it into the message text.
            _logger.LogError(ex, "Request {Endpoint} by user {UserId} failed", endpoint, userId);
            throw;
        }
    }

    private static async Task<string> ReadPayloadAsync(HttpRequest request)
    {
        if (request.ContentLength is null or 0 || !HasJsonBody(request))
        {
            return string.Empty;
        }

        // Buffer so the body stream can be re-read from the start by model binding downstream -
        // Request.Body is forward-only by default.
        request.EnableBuffering();

        using var reader = new StreamReader(
            request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;

        if (body.Length == 0)
        {
            return string.Empty;
        }

        if (Encoding.UTF8.GetByteCount(body) > MaxLoggedBodyBytes)
        {
            return "<payload too large to log>";
        }

        return Redact(body);
    }

    private static bool HasJsonBody(HttpRequest request)
        => request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>Blanks out any field whose name contains "password" or "token" anywhere in the payload before logging.</summary>
    private static string Redact(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            RedactNode(node);
            return node?.ToJsonString() ?? json;
        }
        catch (JsonException)
        {
            // Not valid/object JSON - log the raw text rather than fail the request over it.
            return json;
        }
    }

    private static void RedactNode(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    if (IsSensitiveFieldName(key))
                    {
                        obj[key] = "***REDACTED***";
                    }
                    else
                    {
                        RedactNode(obj[key]);
                    }
                }
                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    RedactNode(item);
                }
                break;
        }
    }

    private static bool IsSensitiveFieldName(string fieldName)
        => RedactedFieldNameParts.Any(part => fieldName.Contains(part, StringComparison.OrdinalIgnoreCase));
}
