using HomeSteadier.Models.Database;
using Microsoft.AspNetCore.Identity;

namespace Homesteadier.API.Auth;

public interface IPasswordUpdateService
{
    /// <summary>
    /// Sets a user's password without requiring the current one (the password-reset path).
    /// Runs the configured password validators first and persists in a single save.
    /// </summary>
    Task<IdentityResult> SetPasswordAsync(User user, string newPassword);
}

/// <summary>
/// Replaces a password outright.
///
/// The obvious <c>UserManager.ResetPasswordAsync</c> is unavailable: it needs a token provider
/// and an <c>IUserSecurityStampStore</c>, and this app's <c>UserStore</c> implements neither.
/// The other obvious route — <c>RemovePasswordAsync</c> then <c>AddPasswordAsync</c> — is worse
/// than unavailable, it is destructive: <c>RemovePasswordAsync</c> sets the hash to null, which
/// <c>UserStore.SetPasswordHashAsync</c> coalesces to an empty string, and then
/// <c>AddPasswordAsync</c>'s "user already has a password" guard (a null check) always trips.
/// The removal has already been committed by that point, leaving the account with an empty
/// hash and no way to log in.
///
/// So: validate, hash, write, save — once.
/// </summary>
public class PasswordUpdateService : IPasswordUpdateService
{
    private readonly UserManager<User> _userManager;
    private readonly IUserPasswordStore<User> _passwordStore;

    public PasswordUpdateService(UserManager<User> userManager, IUserStore<User> userStore)
    {
        _userManager = userManager;

        // AddUserStore<UserStore>() only registers the IUserStore<User> facet, so ask for that
        // and narrow — the same thing UserManager.GetPasswordStore does internally. Injecting
        // IUserPasswordStore<User> directly would compile and then fail to resolve at runtime.
        _passwordStore = userStore as IUserPasswordStore<User>
            ?? throw new InvalidOperationException(
                $"{userStore.GetType().Name} must implement IUserPasswordStore<User> to set passwords.");
    }

    public async Task<IdentityResult> SetPasswordAsync(User user, string newPassword)
    {
        // UserManager runs these itself on the paths that are available to us; on this one we
        // have to invoke them by hand, or the API would accept passwords the sign-up form rejects.
        foreach (var validator in _userManager.PasswordValidators)
        {
            var validationResult = await validator.ValidateAsync(_userManager, user, newPassword);
            if (!validationResult.Succeeded)
            {
                return validationResult;
            }
        }

        var hash = _userManager.PasswordHasher.HashPassword(user, newPassword);
        await _passwordStore.SetPasswordHashAsync(user, hash, CancellationToken.None);

        // Goes through UserManager rather than the store directly so the user validators run;
        // UserStore.UpdateAsync is what stamps updated_at with the DateTimeKind Npgsql wants.
        return await _userManager.UpdateAsync(user);
    }
}
