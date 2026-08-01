using Homesteadier.API.Auth;
using HomeSteadier.Models.Database;
using HomeSteadier.Models.Request.Auth;
using HomeSteadier.Models.Response.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Homesteadier.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<User> _userManager;
    private readonly IJwtTokenService _tokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly RefreshCookieSettings _cookieSettings;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<User> userManager,
        IJwtTokenService tokenService,
        IRefreshTokenService refreshTokenService,
        RefreshCookieSettings cookieSettings,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _refreshTokenService = refreshTokenService;
        _cookieSettings = cookieSettings;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            return Conflict(new { message = "A user with that email already exists." });
        }

        var user = new User
        {
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = "Registration failed.",
                errors = result.Errors.Select(e => new { e.Code, e.Description }),
            });
        }

        var response = await IssueTokensAsync(user);
        return CreatedAtAction(nameof(Me), null, response);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        return Ok(await IssueTokensAsync(user));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh()
    {
        var rawToken = RefreshTokenCookie.Read(Request, _cookieSettings);
        var rotated = rawToken is null ? null : await _refreshTokenService.RotateAsync(rawToken);

        if (rotated is null)
        {
            RefreshTokenCookie.Clear(Response, _cookieSettings);
            return Unauthorized(new { message = "Invalid or expired refresh token." });
        }

        var user = await _userManager.FindByIdAsync(rotated.Value.UserId.ToString());
        if (user is null || !user.IsActive)
        {
            RefreshTokenCookie.Clear(Response, _cookieSettings);
            return Unauthorized(new { message = "Invalid or expired refresh token." });
        }

        RefreshTokenCookie.Append(Response, rotated.Value.NewRawToken, rotated.Value.ExpiresAt, _cookieSettings);

        var (accessToken, expiresAt) = _tokenService.CreateToken(user);
        return Ok(new AuthResponse { Token = accessToken, ExpiresAt = expiresAt, User = user });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var rawToken = RefreshTokenCookie.Read(Request, _cookieSettings);
        if (rawToken is not null)
        {
            await _refreshTokenService.RevokeAsync(rawToken);
        }

        RefreshTokenCookie.Clear(Response, _cookieSettings);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<User>> Me()
    {
        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (userId is null)
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        return Ok(user);
    }

    /// <summary>Issues an access token + a rotating refresh cookie for a freshly-authenticated user.</summary>
    private async Task<AuthResponse> IssueTokensAsync(User user)
    {
        var (accessToken, expiresAt) = _tokenService.CreateToken(user);
        var (rawRefreshToken, refreshExpiresAt) = await _refreshTokenService.IssueAsync(user.Id);

        RefreshTokenCookie.Append(Response, rawRefreshToken, refreshExpiresAt, _cookieSettings);

        return new AuthResponse
        {
            Token = accessToken,
            ExpiresAt = expiresAt,
            User = user,
        };
    }
}
