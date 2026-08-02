using Homesteadier.Repository.Repositories;
using HomeSteadier.Models.Response.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homesteadier.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserRepository userRepository, ILogger<UsersController> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserResponse>>> GetAll()
    {
        try
        {
            var users = await _userRepository.GetAllAsync();
            return Ok(users.Select(UserResponse.FromEntity).ToList());
        }
        catch (Exception ex)
        {
            // Log the detail; don't return ex.Message to the caller.
            _logger.LogError(ex, "Error fetching all users");
            return StatusCode(500, new { message = "Error fetching users" });
        }
    }
}
