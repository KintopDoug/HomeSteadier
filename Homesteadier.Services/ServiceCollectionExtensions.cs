using Homesteadier.Services.Farms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Homesteadier.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the farm services and everything behind them.
    ///
    /// The invitation token service, its settings, and the invitation email template are
    /// <c>internal</c> to this assembly, so the API can't bind or resolve them itself — this call
    /// is the whole wiring surface, and <see cref="IFarmService"/>,
    /// <see cref="IFarmInvitationService"/> and <see cref="IFarmRoleTypeService"/> are the only
    /// ways in.
    /// </summary>
    /// <param name="configuration">Supplies the "FarmInvitation" section.</param>
    public static IServiceCollection AddFarmServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var farmInvitationSettings = new FarmInvitationSettings();
        configuration.GetSection("FarmInvitation").Bind(farmInvitationSettings);
        services.AddSingleton(farmInvitationSettings);

        services.AddScoped<IFarmInvitationTokenService, FarmInvitationTokenService>();

        services.AddScoped<IFarmService, FarmService>();
        services.AddScoped<IFarmRoleTypeService, FarmRoleTypeService>();

        // One FarmInvitationService instance serving both of its interfaces, rather than
        // registering the class twice — a sign-up request resolves the same object for both rather
        // than building its nine-dependency graph a second time.
        services.AddScoped<FarmInvitationService>();
        services.AddScoped<IFarmInvitationService>(sp => sp.GetRequiredService<FarmInvitationService>());
        services.AddScoped<ISignUpInvitationService>(sp => sp.GetRequiredService<FarmInvitationService>());

        return services;
    }
}
