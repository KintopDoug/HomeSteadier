using System.Net;
using HomeSteadier.Models.Request.Farm;
using HomeSteadier.Models.Response.Farm;
using Homesteadier.Tests.Infrastructure;

namespace Homesteadier.Tests;

public class FarmTests
{
    internal static async Task<FarmResponse> CreateFarmAsync(ApiSession owner)
    {
        var response = await owner.PostAsync("/api/Farm", new CreateFarmRequest
        {
            Name = $"Test Farm {Guid.NewGuid():N}",
            Latitude = 40.1m,
            Longitude = -75.2m,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ApiSession.ReadAsync<FarmResponse>(response);
    }

    [Fact]
    public async Task Creating_a_farm_makes_the_creator_its_admin_and_lists_it()
    {
        var factory = await SharedStack.GetAsync();
        var owner = ApiSession.New(factory);
        await owner.SignUpAsync();

        var farm = await CreateFarmAsync(owner);

        Assert.Equal("Admin", farm.RoleName);
        var list = await ApiSession.ReadAsync<List<FarmResponse>>(await owner.GetAsync("/api/Farm"));
        Assert.Contains(list, f => f.Id == farm.Id && f.Name == farm.Name);
    }

    [Fact]
    public async Task A_farm_is_not_visible_to_people_who_are_not_members()
    {
        var factory = await SharedStack.GetAsync();
        var owner = ApiSession.New(factory);
        await owner.SignUpAsync();
        var farm = await CreateFarmAsync(owner);

        var outsider = ApiSession.New(factory);
        await outsider.SignUpAsync();
        var outsiderList = await ApiSession.ReadAsync<List<FarmResponse>>(await outsider.GetAsync("/api/Farm"));

        Assert.DoesNotContain(outsiderList, f => f.Id == farm.Id);
    }

    [Fact]
    public async Task Creating_a_farm_requires_signing_in()
    {
        var factory = await SharedStack.GetAsync();
        var stranger = ApiSession.New(factory);

        var response = await stranger.PostAsync(
            "/api/Farm",
            new CreateFarmRequest { Name = "Nope", Latitude = 1m, Longitude = 2m },
            anonymous: true);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
