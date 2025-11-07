using Microsoft.AspNetCore.Mvc;
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;
using Enterprise.Documentation.Core.Application.Interfaces;

namespace Enterprise.Documentation.Api.Controllers;

public class CreateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class UpdateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserRepository userRepository, IUnitOfWork unitOfWork, ILogger<UsersController> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a new user
    /// </summary>
    /// <param name="request">The user creation details</param>
    /// <returns>The created user</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            _logger.LogInformation("Creating new user: {Email}", request.Email);
            
            var user = new User(
                new UserId(Guid.NewGuid()),
                request.Email,
                $"{request.FirstName} {request.LastName}".Trim(),
                SecurityClearanceLevel.Public,
                new UserId(Guid.Empty), // System user
                request.FirstName,
                request.LastName);
            
            await _userRepository.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();
            
            _logger.LogInformation("User created successfully with ID: {UserId}", user.Id);
            return CreatedAtAction(nameof(GetUser), new { id = user.Id.Value }, user);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid user data provided: {Message}", ex.Message);
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("User creation failed: {Message}", ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while creating user");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
        }
    }

    /// <summary>
    /// Gets a user by ID
    /// </summary>
    /// <param name="id">The user ID</param>
    /// <returns>The user details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUser(Guid id)
    {
        try
        {
            _logger.LogInformation("Retrieving user: {UserId}", id);
            
            var userId = new UserId(id);
            var user = await _userRepository.GetByIdAsync(userId);
            
            if (user == null)
            {
                _logger.LogWarning("User not found: {UserId}", id);
                return NotFound($"User with ID {id} not found.");
            }
            
            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while retrieving user {UserId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
        }
    }

    /// <summary>
    /// Updates an existing user
    /// </summary>
    /// <param name="id">The user ID</param>
    /// <param name="request">The user update details</param>
    /// <returns>The updated user</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        try
        {
            _logger.LogInformation("Updating user: {UserId}", id);
            
            var userId = new UserId(id);
            var user = await _userRepository.GetByIdAsync(userId);
            
            if (user == null)
            {
                _logger.LogWarning("User not found for update: {UserId}", id);
                return NotFound($"User with ID {id} not found.");
            }
            
            // Update user properties
            user.UpdateProfile(
                displayName: $"{request.FirstName} {request.LastName}".Trim(),
                firstName: request.FirstName,
                lastName: request.LastName,
                updatedBy: new UserId(Guid.Empty));
            
            await _userRepository.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();
            
            _logger.LogInformation("User updated successfully: {UserId}", id);
            return Ok(user);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid user update data provided: {Message}", ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while updating user {UserId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
        }
    }
}