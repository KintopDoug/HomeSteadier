using System.Net;
using HomeSteadier.Models.Response.Farm;
using Homesteadier.Tests.Infrastructure;

namespace Homesteadier.Tests;

/// <summary>Proves the harness itself: a real database, migrated and seeded by the API's own startup.</summary>
public class StackSmokeTests
{
    [Fact]
    public async Task The_api_starts_and_reports_healthy()
    {
        var factory = await SharedStack.GetAsync();
        var anyone = ApiSession.New(factory);

        var response = await anyone.GetAsync("/health", anonymous: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Migrations_ran_and_reference_data_was_seeded_into_the_real_database()
    {
        // farm_role_types is seeded from HomeSteadier.Database/Seeds/farm_role_types.csv by the same startup
        // path production uses. If migrations or seeding broke, this is empty or the call fails.
        var factory = await SharedStack.GetAsync();
        var person = ApiSession.New(factory);
        await person.SignUpAsync();

        var response = await person.GetAsync("/api/FarmRoleTypes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var roles = await ApiSession.ReadAsync<List<FarmRoleTypeResponse>>(response);
        Assert.Contains(roles, r => r.Name == "Admin");
        Assert.Contains(roles, r => r.Name == "User");
        Assert.Contains(roles, r => r.Name == "ViewOnly");
    }
}
