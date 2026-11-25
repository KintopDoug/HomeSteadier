using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HomeSteadier.Models.Request.Auth;
using HomeSteadier.Models.Response.Auth;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Homesteadier.Tests.Infrastructure;

/// <summary>
/// One person talking to the API: their own client (and cookie jar), their own email address, and their own
/// "client IP". Every test builds its own sessions, which is what keeps tests independent.
/// </summary>
internal sealed class ApiSession
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory;
    private readonly HttpClient _http;

    public string Email { get; }
    public string Password { get; }
    public string? Token { get; private set; }

    private ApiSession(ApiFactory factory)
    {
        _factory = factory;
        _http = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true, // the refresh token travels as an httpOnly cookie
        });

        // The auth rate limiter partitions by client IP, and the API trusts X-Forwarded-For (it sits behind an
        // ingress in production). A unique value per session keeps tests from sharing a bucket, while the real
        // limiter stays switched on so it can be tested too.
        var b = Guid.NewGuid().ToByteArray();
        _http.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.{b[0]}.{b[1]}.{b[2]}");

        var id = Guid.NewGuid().ToString("N");
        Email = $"it-{id}@example.com";
        Password = $"It!{id[..12]}Aa1";
    }

    public static ApiSession New(ApiFactory factory) => new(factory);

    /// <summary>Registers this person. A successful sign-up also signs them in.</summary>
    public async Task<HttpResponseMessage> SignUpAsync(string? inviteToken = null, string? email = null)
    {
        var response = await _http.PostAsJsonAsync("/api/Auth/SignUp", new RegisterRequest
        {
            Email = email ?? Email,
            Password = Password,
            FirstName = "Integration",
            LastName = "Test",
            InviteToken = inviteToken,
        });
        await RememberTokenAsync(response, HttpStatusCode.Created);
        return response;
    }

    public async Task<HttpResponseMessage> LoginAsync(string? password = null)
    {
        var response = await _http.PostAsJsonAsync("/api/Auth/Login", new LoginRequest
        {
            Email = Email,
            Password = password ?? Password,
        });
        await RememberTokenAsync(response, HttpStatusCode.OK);
        return response;
    }

    /// <summary>Exchanges the refresh cookie from the last sign-in for a new access token.</summary>
    public async Task<HttpResponseMessage> RefreshAsync()
    {
        var response = await _http.PostAsync("/api/Auth/Refresh", content: null);
        await RememberTokenAsync(response, HttpStatusCode.OK);
        return response;
    }

    /// <summary>
    /// The refresh token value a response set in its cookie, or null if it did not set one (or cleared it).
    /// </summary>
    public static string? RefreshCookieFrom(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return null;
        }

        foreach (var header in values)
        {
            const string prefix = "refreshToken=";
            if (header.StartsWith(prefix, StringComparison.Ordinal))
            {
                var value = header[prefix.Length..].Split(';')[0];
                return value.Length == 0 ? null : value;
            }
        }

        return null;
    }

    /// <summary>
    /// Presents a specific refresh token value (e.g. one captured earlier) from a client with no cookie jar of its
    /// own, so the cookie this session holds is neither used nor replaced.
    /// </summary>
    public async Task<HttpResponseMessage> RefreshWithAsync(string rawRefreshToken)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        client.DefaultRequestHeaders.Add("X-Forwarded-For", _http.DefaultRequestHeaders.GetValues("X-Forwarded-For").Single());

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/Refresh");
        request.Headers.Add("Cookie", $"refreshToken={rawRefreshToken}");
        var response = await client.SendAsync(request);
        // Buffer so the response outlives the client.
        await response.Content.LoadIntoBufferAsync();
        return response;
    }

    /// <summary>Calls any endpoint; sends this person's access token unless <paramref name="anonymous"/>.</summary>
    public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null, bool anonymous = false)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        if (!anonymous && Token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        }

        return _http.SendAsync(request);
    }

    public Task<HttpResponseMessage> GetAsync(string path, bool anonymous = false) =>
        SendAsync(HttpMethod.Get, path, anonymous: anonymous);

    public Task<HttpResponseMessage> PostAsync(string path, object? body = null, bool anonymous = false) =>
        SendAsync(HttpMethod.Post, path, body, anonymous);

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(Json);
        return value ?? throw new InvalidOperationException($"Empty or null {typeof(T).Name} in the response.");
    }

    private async Task RememberTokenAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode != expected)
        {
            return;
        }

        var auth = await ReadAsync<AuthResponse>(response);
        Token = auth.Token;
    }
}
