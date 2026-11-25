using System.Net;
using HomeSteadier.Models.Request.Farm;
using HomeSteadier.Models.Response.Farm;
using Homesteadier.Tests.Infrastructure;

namespace Homesteadier.Tests;

public class FarmInvitationTests
{
    private static string UniqueAddress() => $"invitee-{Guid.NewGuid():N}@example.com";

    private static async Task<FarmRoleTypeResponse> RoleAsync(ApiSession session, string name)
    {
        var roles = await ApiSession.ReadAsync<List<FarmRoleTypeResponse>>(await session.GetAsync("/api/FarmRoleTypes"));
        return roles.Single(r => r.Name == name);
    }

    [Fact]
    public async Task Only_a_farm_admin_can_invite_and_an_invited_member_cannot()
    {
        var factory = await SharedStack.GetAsync();

        var admin = ApiSession.New(factory);
        await admin.SignUpAsync();
        var farm = await FarmTests.CreateFarmAsync(admin);
        var userRole = await RoleAsync(admin, "User");

        var member = ApiSession.New(factory);
        await member.SignUpAsync(); // has an account already, so the invitation can be accepted directly

        // The admin invites the member.
        var invite = await admin.PostAsync("/api/FarmInvitations", new CreateFarmInvitationRequest
        {
            FarmId = farm.Id,
            Email = member.Email,
            FarmRoleTypeId = userRole.Id,
        });
        Assert.Equal(HttpStatusCode.Accepted, invite.StatusCode);

        // The member follows the emailed link (the email is captured, not sent).
        var token = factory.Emails.InvitationTokenFor(member.Email);
        var accept = await member.PostAsync($"/api/FarmInvitations/{Uri.EscapeDataString(token)}/accept", anonymous: true);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        // They are now on the farm, as a plain User, not an Admin.
        var memberFarms = await ApiSession.ReadAsync<List<FarmResponse>>(await member.GetAsync("/api/Farm"));
        var joined = Assert.Single(memberFarms, f => f.Id == farm.Id);
        Assert.Equal("User", joined.RoleName);

        // A non-admin may not invite anyone...
        var denied = await member.PostAsync("/api/FarmInvitations", new CreateFarmInvitationRequest
        {
            FarmId = farm.Id,
            Email = UniqueAddress(),
            FarmRoleTypeId = userRole.Id,
        });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        // ...while the admin still can.
        var allowed = await admin.PostAsync("/api/FarmInvitations", new CreateFarmInvitationRequest
        {
            FarmId = farm.Id,
            Email = UniqueAddress(),
            FarmRoleTypeId = userRole.Id,
        });
        Assert.Equal(HttpStatusCode.Accepted, allowed.StatusCode);
    }

    [Fact]
    public async Task Inviting_requires_signing_in()
    {
        var factory = await SharedStack.GetAsync();
        var stranger = ApiSession.New(factory);

        var response = await stranger.PostAsync(
            "/api/FarmInvitations",
            new CreateFarmInvitationRequest { FarmId = 1, Email = UniqueAddress(), FarmRoleTypeId = 1 },
            anonymous: true);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_invitation_token_is_rejected()
    {
        var factory = await SharedStack.GetAsync();
        var stranger = ApiSession.New(factory);

        var response = await stranger.PostAsync("/api/FarmInvitations/not-a-real-token/accept", anonymous: true);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
