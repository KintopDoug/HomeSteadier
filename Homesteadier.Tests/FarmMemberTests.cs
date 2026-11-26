using System.Net;
using HomeSteadier.Models.Request.Farm;
using HomeSteadier.Models.Response.Farm;
using Homesteadier.Tests.Infrastructure;

namespace Homesteadier.Tests;

public class FarmMemberTests
{
    private static async Task<FarmRoleTypeResponse> RoleAsync(ApiSession s, string name)
    {
        var roles = await ApiSession.ReadAsync<List<FarmRoleTypeResponse>>(await s.GetAsync("/api/FarmRoleTypes"));
        return roles.Single(r => r.Name == name);
    }

    private static async Task<int> UserIdOnAsync(ApiSession viewer, int farmId, string email)
    {
        var members = await ApiSession.ReadAsync<List<FarmMemberResponse>>(await viewer.GetAsync($"/api/Farm/{farmId}/members"));
        return members.Single(m => m.Email == email).UserId;
    }

    private static async Task AddMemberAsync(ApiSession admin, ApiSession member, ApiFactory factory, int farmId, int roleId)
    {
        await member.SignUpAsync();
        var invite = await admin.PostAsync("/api/FarmInvitations", new CreateFarmInvitationRequest
        {
            FarmId = farmId, Email = member.Email, FarmRoleTypeId = roleId,
        });
        Assert.Equal(HttpStatusCode.Accepted, invite.StatusCode);
        var token = factory.Emails.InvitationTokenFor(member.Email);
        var accept = await member.PostAsync($"/api/FarmInvitations/{Uri.EscapeDataString(token)}/accept", anonymous: true);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
    }

    [Fact]
    public async Task Admin_can_list_change_role_and_remove_members_and_a_plain_member_cannot()
    {
        var factory = await SharedStack.GetAsync();
        var admin = ApiSession.New(factory);
        await admin.SignUpAsync();
        var farm = await FarmTests.CreateFarmAsync(admin);
        var userRole = await RoleAsync(admin, "User");
        var adminRole = await RoleAsync(admin, "Admin");

        var member = ApiSession.New(factory);
        await AddMemberAsync(admin, member, factory, farm.Id, userRole.Id);

        var members = await ApiSession.ReadAsync<List<FarmMemberResponse>>(await admin.GetAsync($"/api/Farm/{farm.Id}/members"));
        Assert.Equal(2, members.Count);
        var row = Assert.Single(members, m => m.Email == member.Email);
        Assert.Equal("User", row.RoleName);
        Assert.False(string.IsNullOrEmpty(row.FirstName));

        // Denial paths: a plain member and anonymous callers.
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync($"/api/Farm/{farm.Id}/members")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync($"/api/Farm/{farm.Id}/members", anonymous: true)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.SendAsync(HttpMethod.Put,
            $"/api/Farm/{farm.Id}/members/{row.UserId}", new UpdateFarmMemberRoleRequest { FarmRoleTypeId = adminRole.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.SendAsync(HttpMethod.Delete,
            $"/api/Farm/{farm.Id}/members/{row.UserId}")).StatusCode);

        // Promote, check outcome.
        var promote = await admin.SendAsync(HttpMethod.Put, $"/api/Farm/{farm.Id}/members/{row.UserId}",
            new UpdateFarmMemberRoleRequest { FarmRoleTypeId = adminRole.Id });
        Assert.Equal(HttpStatusCode.NoContent, promote.StatusCode);
        members = await ApiSession.ReadAsync<List<FarmMemberResponse>>(await admin.GetAsync($"/api/Farm/{farm.Id}/members"));
        Assert.Equal("Admin", members.Single(m => m.UserId == row.UserId).RoleName);

        // Bad role.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.SendAsync(HttpMethod.Put,
            $"/api/Farm/{farm.Id}/members/{row.UserId}", new UpdateFarmMemberRoleRequest { FarmRoleTypeId = 999999 })).StatusCode);

        // Remove; they lose access.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.SendAsync(HttpMethod.Delete,
            $"/api/Farm/{farm.Id}/members/{row.UserId}")).StatusCode);
        members = await ApiSession.ReadAsync<List<FarmMemberResponse>>(await admin.GetAsync($"/api/Farm/{farm.Id}/members"));
        Assert.DoesNotContain(members, m => m.UserId == row.UserId);
        var farms = await ApiSession.ReadAsync<List<FarmResponse>>(await member.GetAsync("/api/Farm"));
        Assert.DoesNotContain(farms, f => f.Id == farm.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.SendAsync(HttpMethod.Delete,
            $"/api/Farm/{farm.Id}/members/{row.UserId}")).StatusCode);
    }

    [Fact]
    public async Task The_last_admin_cannot_be_demoted_or_removed_and_outsiders_are_refused()
    {
        var factory = await SharedStack.GetAsync();
        var admin = ApiSession.New(factory);
        await admin.SignUpAsync();
        var farm = await FarmTests.CreateFarmAsync(admin);
        var userRole = await RoleAsync(admin, "User");
        var selfId = await UserIdOnAsync(admin, farm.Id, admin.Email);

        Assert.Equal(HttpStatusCode.Conflict, (await admin.SendAsync(HttpMethod.Put,
            $"/api/Farm/{farm.Id}/members/{selfId}", new UpdateFarmMemberRoleRequest { FarmRoleTypeId = userRole.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.SendAsync(HttpMethod.Delete,
            $"/api/Farm/{farm.Id}/members/{selfId}")).StatusCode);

        var outsider = ApiSession.New(factory);
        await outsider.SignUpAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/Farm/{farm.Id}/members")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.SendAsync(HttpMethod.Delete,
            $"/api/Farm/{farm.Id}/members/{selfId}")).StatusCode);
    }
}
