using System.Net;
using HomeSteadier.Models.Response.Users;
using Homesteadier.API.Auth;
using Homesteadier.Tests.Infrastructure;

namespace Homesteadier.Tests;

public class AuthTests
{
    [Fact]
    public async Task Sign_up_login_and_refresh_each_issue_a_working_session()
    {
        var factory = await SharedStack.GetAsync();
        var person = ApiSession.New(factory);

        var signUp = await person.SignUpAsync();
        Assert.Equal(HttpStatusCode.Created, signUp.StatusCode);
        Assert.False(string.IsNullOrEmpty(person.Token));

        var login = await person.LoginAsync();
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        // The access token really authenticates, and it belongs to this person.
        var me = await person.GetAsync("/api/Auth/Me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var user = await ApiSession.ReadAsync<UserResponse>(me);
        Assert.Equal(person.Email, user.Email, ignoreCase: true);

        // Login set the httpOnly refresh cookie; presenting it yields an access token that really authenticates.
        var refresh = await person.RefreshAsync();
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var meAfterRefresh = await person.GetAsync("/api/Auth/Me");
        Assert.Equal(HttpStatusCode.OK, meAfterRefresh.StatusCode);

        // Refresh tokens rotate: the cookie from that response must work for the next refresh too.
        var refreshAgain = await person.RefreshAsync();
        Assert.Equal(HttpStatusCode.OK, refreshAgain.StatusCode);
    }

    [Fact]
    public async Task A_wrong_password_is_rejected()
    {
        var factory = await SharedStack.GetAsync();
        var person = ApiSession.New(factory);
        await person.SignUpAsync();

        var login = await person.LoginAsync(password: "not-my-password");

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Signing_up_twice_with_the_same_email_is_a_conflict()
    {
        var factory = await SharedStack.GetAsync();
        var person = ApiSession.New(factory);
        Assert.Equal(HttpStatusCode.Created, (await person.SignUpAsync()).StatusCode);

        var again = await person.SignUpAsync();

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Refreshing_without_a_refresh_cookie_is_rejected()
    {
        var factory = await SharedStack.GetAsync();
        var stranger = ApiSession.New(factory); // never signed in, so no cookie

        var refresh = await stranger.RefreshAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoints_turn_anonymous_callers_away()
    {
        var factory = await SharedStack.GetAsync();
        var stranger = ApiSession.New(factory);

        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync("/api/Farm", anonymous: true)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync("/api/Auth/Me", anonymous: true)).StatusCode);
    }

    [Fact]
    public async Task The_login_rate_limit_blocks_one_client_without_affecting_another()
    {
        // The real limiter (not a test double): AuthRateLimiting.PermitLimit attempts per minute per client IP.
        var factory = await SharedStack.GetAsync();
        var attacker = ApiSession.New(factory);

        for (var attempt = 1; attempt <= AuthRateLimiting.PermitLimit; attempt++)
        {
            var response = await attacker.LoginAsync(password: "guess");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var blocked = await attacker.LoginAsync(password: "guess");
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);

        // A different client address has its own bucket.
        var bystander = ApiSession.New(factory);
        var unaffected = await bystander.LoginAsync(password: "guess");
        Assert.Equal(HttpStatusCode.Unauthorized, unaffected.StatusCode);
    }
}
