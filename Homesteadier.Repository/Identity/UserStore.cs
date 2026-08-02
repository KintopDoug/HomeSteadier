using System.Globalization;
using HomeSteadier.Models.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Homesteadier.Repository.Identity;

/// <summary>
/// ASP.NET Core Identity store implemented directly over the existing <c>users</c> table
/// (via <see cref="HomesteadierDbContext"/>) rather than the Identity EF schema. This lets
/// <see cref="UserManager{TUser}"/> hash passwords into the existing <c>password</c> column
/// and look users up by email, without introducing AspNetUsers or new columns.
///
/// The table has no normalized-value columns, so normalization is computed on the fly and the
/// Set*Normalized* methods are intentionally no-ops.
/// </summary>
public class UserStore :
    IUserStore<User>,
    IUserPasswordStore<User>,
    IUserEmailStore<User>
{
    private readonly HomesteadierDbContext _context;

    public UserStore(HomesteadierDbContext context)
    {
        _context = context;
    }

    private static string Normalize(string value) => value.ToUpper(CultureInfo.InvariantCulture);

    // IUserStore ---------------------------------------------------------------------------

    public Task<string> GetUserIdAsync(User user, CancellationToken cancellationToken)
        => Task.FromResult(user.Id.ToString(CultureInfo.InvariantCulture));

    public Task<string?> GetUserNameAsync(User user, CancellationToken cancellationToken)
        => Task.FromResult<string?>(user.Email);

    public Task SetUserNameAsync(User user, string? userName, CancellationToken cancellationToken)
    {
        // Username and email are the same thing for this app.
        user.Email = userName ?? string.Empty;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(User user, CancellationToken cancellationToken)
        => Task.FromResult<string?>(Normalize(user.Email));

    public Task SetNormalizedUserNameAsync(User user, string? normalizedName, CancellationToken cancellationToken)
        => Task.CompletedTask; // No normalized column; recomputed on read.

    public async Task<IdentityResult> CreateAsync(User user, CancellationToken cancellationToken)
    {
        // Set IsActive explicitly so EF doesn't treat the CLR default (false) as "unset"
        // and fall back to the DB default. CreatedAt/UpdatedAt are left unset so the
        // CURRENT_TIMESTAMP column defaults apply.
        user.IsActive = true;
        _context.Users.Add(user);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueEmailViolation(ex))
        {
            // The application-level pre-check (AuthController) can race: two concurrent
            // sign-ups for the same email (or case-variants of it) can both pass the check
            // before either commits. ix_users_email_upper is what actually closes that race;
            // without this catch, the losing request would surface as an unhandled 500
            // instead of the same "already exists" outcome the pre-check normally returns.
            return IdentityResult.Failed(new IdentityError
            {
                Code = nameof(IdentityErrorDescriber.DuplicateEmail),
                Description = "A user with that email already exists.",
            });
        }

        return IdentityResult.Success;
    }

    private static bool IsUniqueEmailViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    public async Task<IdentityResult> UpdateAsync(User user, CancellationToken cancellationToken)
    {
        // The updated_at column is "timestamp without time zone"; Npgsql rejects a Utc-kind value
        // for that type, so store the UTC wall-clock as Unspecified (matching the CURRENT_TIMESTAMP default).
        user.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        _context.Users.Update(user);
        await _context.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(User user, CancellationToken cancellationToken)
    {
        _context.Users.Remove(user);
        await _context.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }

    public async Task<User?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        if (!int.TryParse(userId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
        {
            return null;
        }

        return await _context.Users.FindAsync([id], cancellationToken);
    }

    public Task<User?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
        => FindByEmailAsync(normalizedUserName, cancellationToken);

    // IUserPasswordStore -------------------------------------------------------------------

    public Task SetPasswordHashAsync(User user, string? passwordHash, CancellationToken cancellationToken)
    {
        user.Password = passwordHash ?? string.Empty;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(User user, CancellationToken cancellationToken)
        => Task.FromResult<string?>(user.Password);

    public Task<bool> HasPasswordAsync(User user, CancellationToken cancellationToken)
        => Task.FromResult(!string.IsNullOrEmpty(user.Password));

    // IUserEmailStore ----------------------------------------------------------------------

    public Task SetEmailAsync(User user, string? email, CancellationToken cancellationToken)
    {
        user.Email = email ?? string.Empty;
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(User user, CancellationToken cancellationToken)
        => Task.FromResult<string?>(user.Email);

    // No email-confirmation column; treat all users as confirmed so login isn't blocked.
    public Task<bool> GetEmailConfirmedAsync(User user, CancellationToken cancellationToken)
        => Task.FromResult(true);

    public Task SetEmailConfirmedAsync(User user, bool confirmed, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public async Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
        => await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToUpper() == normalizedEmail, cancellationToken);

    public Task<string?> GetNormalizedEmailAsync(User user, CancellationToken cancellationToken)
        => Task.FromResult<string?>(Normalize(user.Email));

    public Task SetNormalizedEmailAsync(User user, string? normalizedEmail, CancellationToken cancellationToken)
        => Task.CompletedTask; // No normalized column; recomputed on read.

    // IDisposable --------------------------------------------------------------------------

    public void Dispose()
    {
        // DbContext lifetime is managed by DI; nothing to dispose here.
    }
}
