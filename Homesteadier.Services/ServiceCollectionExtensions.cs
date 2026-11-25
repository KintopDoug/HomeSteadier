using Homesteadier.Services.Auth;
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

    /// <summary>
    /// Registers the auth services: <see cref="IAuthService"/> and <see cref="IPasswordService"/> are the
    /// only ways in; the token services, session service and password-reset settings are internal.
    ///
    /// Needs <c>UserManager&lt;User&gt;</c> (Identity), <see cref="FrontendUrls"/>, an
    /// <c>IEmailSender</c> and the farm services registered by the caller.
    /// </summary>
    /// <param name="configuration">Supplies the "PasswordReset" section.</param>
    /// <param name="jwtSettings">Already bound and validated by the caller, which also needs it for bearer validation.</param>
    public static IServiceCollection AddAuthServices(
        this IServiceCollection services,
        IConfiguration configuration,
        JwtSettings jwtSettings)
    {
        services.AddSingleton(jwtSettings);

        var passwordResetSettings = new PasswordResetSettings();
        configuration.GetSection("PasswordReset").Bind(passwordResetSettings);
        services.AddSingleton(passwordResetSettings);

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IPasswordResetTokenService, PasswordResetTokenService>();
        services.AddScoped<IPasswordUpdateService, PasswordUpdateService>();
        services.AddScoped<ISessionService, SessionService>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPasswordService, PasswordService>();

        return services;
    }
}
