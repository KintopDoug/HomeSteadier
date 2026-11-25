using System.Net;
using HomeSteadier.Models.Request.Auth;
using HomeSteadier.Models.Request.Farm;
using HomeSteadier.Models.Response.Farm;
using Homesteadier.Repository;
using Homesteadier.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Homesteadier.Tests;

/// <summary>
/// Pins the observable behaviour of the auth endpoints (sessions, password reset/change, invitation sign-up) so
/// the logic can be moved out of the controller without changing it.
/// </summary>
public class AuthFlowTests
{
    private const string NewPassword = "Brand-New-Passw0rd!";

    private static async Task DeactivateAsync(ApiFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HomesteadierDbContext>();
        var changed = await db.Users.Where(u => u.Email == email)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        Assert.Equal(1, changed);
    }

    private static async Task<ApiSession> SignedUpAsync(ApiFactory factory)
    {
        var person = ApiSession.New(factory);
        Assert.Equal(HttpStatusCode.Created, (await person.SignUpAsync()).StatusCode);
        return person;
    }

    private static Task<HttpResponseMessage> ForgotAsync(ApiSession session, string email) =>
        session.PostAsync("/api/Auth/ForgotPassword", new ForgotPasswordRequest { Email = email }, anonymous: true);

    private static Task<HttpResponseMessage> ResetAsync(ApiSession session, string token, string password) =>
        session.PostAsync("/api/Auth/ResetPassword", new ResetPasswordRequest { Token = token, NewPassword = password }, anonymous: true);

    // ---- Logout ----

    [Fact]
    public async Task Logout_revokes_the_refresh_token_and_clears_the_cookie()
    {
        var factory = await SharedStack.GetAsync();
        var person = ApiSession.New(factory);
        var signUp = await person.SignUpAsync();
        var cookie = ApiSession.RefreshCookieFrom(signUp);
        Assert.NotNull(cookie);

        var logout = await person.PostAsync("/api/Auth/Logout", anonymous: true);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Null(ApiSession.RefreshCookieFrom(logout));
        Assert.Equal(HttpStatusCode.Unauthorized, (await person.RefreshWithAsync(cookie!)).StatusCode);
    }

    // ---- Forgot password ----

    [Fact]
    public async Task Forgot_password_emails_a_known_account_and_stays_silent_for_unknown_ones()
    {
        var factory = await SharedStack.GetAsync();
        var person = await SignedUpAsync(factory);
        var stranger = ApiSession.New(factory);

        var known = await ForgotAsync(person, person.Email);
        var unknown = await ForgotAsync(person, stranger.Email);

        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(1, factory.Emails.PasswordResetCountFor(person.Email));
        Assert.Equal(0, factory.Emails.PasswordResetCountFor(stranger.Email));
    }

    [Fact]
    public async Task Forgot_password_for_a_deactivated_account_returns_202_and_sends_nothing()
    {
        var factory = await SharedStack.GetAsync();
        var person = await SignedUpAsync(factory);
        await DeactivateAsync(factory, person.Email);

        var response = await ForgotAsync(person, person.Email);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(0, factory.Emails.PasswordResetCountFor(person.Email));
    }

    // ---- Reset password ----

    [Fact]
    public async Task Reset_password_sets_the_password_signs_in_and_revokes_earlier_sessions()
    {
        var factory = await SharedStack.GetAsync();
        var person = ApiSession.New(factory);
        var oldCookie = ApiSession.RefreshCookieFrom(await person.SignUpAsync());
        Assert.NotNull(oldCookie);
        await ForgotAsync(person, person.Email);
        var token = factory.Emails.PasswordResetTokenFor(person.Email);

        var reset = await ResetAsync(person, token, NewPassword);

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        var newCookie = ApiSession.RefreshCookieFrom(reset);
        Assert.NotNull(newCookie);
        // The reset response is a real sign-in...
        Assert.Equal(HttpStatusCode.OK, (await person.RefreshWithAsync(newCookie!)).StatusCode);
        // ...the old session is gone, the old password no longer works, the new one does.
        Assert.Equal(HttpStatusCode.Unauthorized, (await person.RefreshWithAsync(oldCookie!)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await person.LoginAsync()).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await person.LoginAsync(NewPassword)).StatusCode);
    }

    [Fact]
    public async Task A_reset_link_cannot_be_reused_and_an_invalid_one_is_rejected()
    {
        var factory = await SharedStack.GetAsync();
        var person = await SignedUpAsync(factory);
        await ForgotAsync(person, person.Email);
        var token = factory.Emails.PasswordResetTokenFor(person.Email);
        Assert.Equal(HttpStatusCode.OK, (await ResetAsync(person, token, NewPassword)).StatusCode);

        var reused = await ResetAsync(person, token, "Another-Passw0rd!");
        var invalid = await ResetAsync(person, "not-a-real-token", NewPassword);

        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await person.LoginAsync(NewPassword)).StatusCode);
    }

    [Fact]
    public async Task Only_the_newest_reset_link_works()
    {
        var factory = await SharedStack.GetAsync();
        var person = await SignedUpAsync(factory);
        await ForgotAsync(person, person.Email);
        var first = factory.Emails.PasswordResetTokenFor(person.Email);
        await ForgotAsync(person, person.Email);
        var second = factory.Emails.PasswordResetTokenFor(person.Email);

        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(person, first, NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ResetAsync(person, second, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task A_rejected_new_password_leaves_the_reset_link_usable()
    {
        var factory = await SharedStack.GetAsync();
        var person = await SignedUpAsync(factory);
        await ForgotAsync(person, person.Email);
        var token = factory.Emails.PasswordResetTokenFor(person.Email);

        var rejected = await ResetAsync(person, token, "short");
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Contains("Password reset failed.", await rejected.Content.ReadAsStringAsync());
        // The password did not change.
        Assert.Equal(HttpStatusCode.OK, (await person.LoginAsync()).StatusCode);

        var retry = await ResetAsync(person, token, NewPassword);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }

    [Fact]
    public async Task A_reset_link_for_a_deactivated_account_is_rejected()
    {
        var factory = await SharedStack.GetAsync();
        var person = await SignedUpAsync(factory);
        await ForgotAsync(person, person.Email);
        var token = factory.Emails.PasswordResetTokenFor(person.Email);
        await DeactivateAsync(factory, person.Email);

        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(person, token, NewPassword)).StatusCode);
    }

    // ---- Change password ----

    [Fact]
    public async Task Change_password_succeeds_and_revokes_other_sessions_but_keeps_the_callers()
    {
        var factory = await SharedStack.GetAsync();
        var person = ApiSession.New(factory);
        var otherDeviceCookie = ApiSession.RefreshCookieFrom(await person.SignUpAsync());
        Assert.NotNull(otherDeviceCookie);

        var change = await person.PostAsync("/api/Auth/ChangePassword", new ChangePasswordRequest
        {
            CurrentPassword = person.Password,
            NewPassword = NewPassword,
        });

        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        var callerCookie = ApiSession.RefreshCookieFrom(change);
        Assert.NotNull(callerCookie);
        Assert.Equal(HttpStatusCode.OK, (await person.RefreshWithAsync(callerCookie!)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await person.RefreshWithAsync(otherDeviceCookie!)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await person.LoginAsync()).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await person.LoginAsync(NewPassword)).StatusCode);
    }

    [Fact]
    public async Task Change_password_with_the_wrong_current_password_is_rejected()
    {
        var factory = await SharedStack.GetAsync();
        var person = await SignedUpAsync(factory);

        var change = await person.PostAsync("/api/Auth/ChangePassword", new ChangePasswordRequest
        {
            CurrentPassword = "not-my-password",
            NewPassword = NewPassword,
        });

        Assert.Equal(HttpStatusCode.BadRequest, change.StatusCode);
        Assert.Contains("Your current password is incorrect.", await change.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await person.LoginAsync()).StatusCode);
    }

    [Fact]
    public async Task Change_password_requires_signing_in()
    {
        var factory = await SharedStack.GetAsync();
        var stranger = ApiSession.New(factory);

        var change = await stranger.PostAsync("/api/Auth/ChangePassword", new ChangePasswordRequest
        {
            CurrentPassword = "x",
            NewPassword = NewPassword,
        }, anonymous: true);

        Assert.Equal(HttpStatusCode.Unauthorized, change.StatusCode);
    }

    [Fact]
    public async Task Changing_the_password_invalidates_an_outstanding_reset_link()
    {
        var factory = await SharedStack.GetAsync();
        var person = await SignedUpAsync(factory);
        await ForgotAsync(person, person.Email);
        var token = factory.Emails.PasswordResetTokenFor(person.Email);

        var change = await person.PostAsync("/api/Auth/ChangePassword", new ChangePasswordRequest
        {
            CurrentPassword = person.Password,
            NewPassword = NewPassword,
        });
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(person, token, "Yet-Another-Passw0rd!")).StatusCode);
    }

    // ---- Sign-up with an invitation ----

    private static async Task<string> InviteAsync(ApiFactory factory, string address)
    {
        var admin = await SignedUpAsync(factory);
        var farm = await FarmTests.CreateFarmAsync(admin);
        var roles = await ApiSession.ReadAsync<List<FarmRoleTypeResponse>>(await admin.GetAsync("/api/FarmRoleTypes"));
        var invite = await admin.PostAsync("/api/FarmInvitations", new CreateFarmInvitationRequest
        {
            FarmId = farm.Id,
            Email = address,
            FarmRoleTypeId = roles.Single(r => r.Name == "User").Id,
        });
        Assert.Equal(HttpStatusCode.Accepted, invite.StatusCode);
        return factory.Emails.InvitationTokenFor(address);
    }

    [Fact]
    public async Task Signing_up_with_a_valid_invitation_joins_the_farm()
    {
        var factory = await SharedStack.GetAsync();
        var invitee = ApiSession.New(factory);
        var token = await InviteAsync(factory, invitee.Email);

        var signUp = await invitee.SignUpAsync(inviteToken: token);

        Assert.Equal(HttpStatusCode.Created, signUp.StatusCode);
        var farms = await ApiSession.ReadAsync<List<FarmResponse>>(await invitee.GetAsync("/api/Farm"));
        var joined = Assert.Single(farms);
        Assert.Equal("User", joined.RoleName);
    }

    [Fact]
    public async Task Signing_up_with_an_invalid_invitation_fails_and_creates_no_account()
    {
        var factory = await SharedStack.GetAsync();
        var person = ApiSession.New(factory);

        var signUp = await person.SignUpAsync(inviteToken: "not-a-real-token");

        Assert.Equal(HttpStatusCode.BadRequest, signUp.StatusCode);
        Assert.Contains("This invitation is invalid or has expired.", await signUp.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await person.LoginAsync()).StatusCode);
    }

    [Fact]
    public async Task Signing_up_with_an_invitation_for_another_address_fails_and_creates_no_account()
    {
        var factory = await SharedStack.GetAsync();
        var invited = ApiSession.New(factory);
        var someoneElse = ApiSession.New(factory);
        var token = await InviteAsync(factory, invited.Email);

        var signUp = await someoneElse.SignUpAsync(inviteToken: token);

        Assert.Equal(HttpStatusCode.BadRequest, signUp.StatusCode);
        Assert.Contains("issued to a different email address", await signUp.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await someoneElse.LoginAsync()).StatusCode);
    }

    // ---- Refresh tokens ----

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_revokes_the_users_active_tokens()
    {
        var factory = await SharedStack.GetAsync();
        var person = ApiSession.New(factory);
        var original = ApiSession.RefreshCookieFrom(await person.SignUpAsync());
        Assert.NotNull(original);

        var rotated = await person.RefreshAsync(); // rotates; the session's cookie jar now holds the new token
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var current = ApiSession.RefreshCookieFrom(rotated);
        Assert.NotNull(current);

        // Replaying the rotated-away token is treated as theft...
        var replay = await person.RefreshWithAsync(original!);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        // ...and takes the otherwise-valid current token down with it.
        Assert.Equal(HttpStatusCode.Unauthorized, (await person.RefreshWithAsync(current!)).StatusCode);
    }

    [Fact]
    public async Task A_deactivated_user_cannot_log_in_or_refresh()
    {
        var factory = await SharedStack.GetAsync();
        var person = ApiSession.New(factory);
        var cookie = ApiSession.RefreshCookieFrom(await person.SignUpAsync());
        Assert.NotNull(cookie);
        await DeactivateAsync(factory, person.Email);

        Assert.Equal(HttpStatusCode.Unauthorized, (await person.LoginAsync()).StatusCode);

        var refresh = await person.RefreshWithAsync(cookie!);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task A_failed_refresh_clears_the_cookie()
    {
        var factory = await SharedStack.GetAsync();
        var person = ApiSession.New(factory);

        var refresh = await person.RefreshWithAsync("not-a-real-token");

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.True(refresh.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies!, c => c.StartsWith("refreshToken=;", StringComparison.Ordinal));
    }
}
