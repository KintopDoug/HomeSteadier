//using Homesteadier.Repository.Repositories;
//using HomeSteadier.Models.Database;
//using Microsoft.AspNetCore.Mvc;

//namespace Homesteadier.API.Controllers;

//[ApiController]
//[Route("api/[controller]")]
//public class UsersController : ControllerBase
//{
//    private readonly IUserRepository _userRepository;
//    private readonly ILogger<UsersController> _logger;
//    private readonly ITestRepository _testRepository;

//    public UsersController(IUserRepository userRepository, ITestRepository testRepository, ILogger<UsersController> logger)
//    {
//        _userRepository = userRepository;
//        _logger = logger;
//        _testRepository = testRepository;
//    }

//    [HttpGet]
//    public async Task<ActionResult<List<Test>>> GetAll()
//    {
//        try
//        {
//            var users = await _userRepository.GetAllAsync();
//            var tests = await _testRepository.GetAllAsync();
//            return Ok(tests);
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Error fetching all users");
//            return StatusCode(500, new { message = "Error fetching users", error = ex.Message });
//        }
//    }
//}
