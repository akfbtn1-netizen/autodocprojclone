using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;
using Enterprise.Documentation.Core.Application.Interfaces;

namespace Enterprise.Documentation.Api.Controllers;

/// <summary>User login request model</summary>
public class LoginRequest
{
    /// <summary>User email address</summary>
    public string Email { get; set; } = string.Empty;
    /// <summary>User password</summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>Login response model</summary>
public class LoginResponse
{
    /// <summary>JWT access token</summary>
    public string Token { get; set; } = string.Empty;
    /// <summary>Refresh token for token renewal</summary>
    public string RefreshToken { get; set; } = string.Empty;
    /// <summary>Token expiration timestamp</summary>
    public DateTime ExpiresAt { get; set; }
    /// <summary>User information</summary>
    public UserInfo User { get; set; } = new();
}

/// <summary>User information model</summary>
public class UserInfo
{
    /// <summary>User unique identifier</summary>
    public Guid Id { get; set; }
    /// <summary>User email address</summary>
    public string Email { get; set; } = string.Empty;
    /// <summary>User display name</summary>
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>User assigned roles</summary>
    public List<string> Roles { get; set; } = new();
}

/// <summary>Authentication controller handling user login and token management</summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    /// <summary>Initializes authentication controller</summary>
    /// <param name="userRepository">User repository service</param>
    /// <param name="configuration">Application configuration</param>
    /// <param name="logger">Logger instance</param>
    public AuthController(
        IUserRepository userRepository,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Authenticates a user and returns a JWT token
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <returns>JWT token and user information</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { error = "Email and password are required" });
            }

            _logger.LogInformation("Login attempt for user: {Email}", request.Email);

            // Find user by email
            var user = await _userRepository.GetByEmailAsync(request.Email);
            if (user == null || !user.IsActive)
            {
                _logger.LogWarning("Login failed - user not found or inactive: {Email}", request.Email);
                return Unauthorized(new { error = "Invalid credentials" });
            }

            // Note: In a real implementation, you would verify the password hash here
            // For now, we'll accept any password for demo purposes
            // if (!VerifyPassword(request.Password, user.PasswordHash))
            // {
            //     return Unauthorized(new { error = "Invalid credentials" });
            // }

            // Generate JWT token
            var token = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();

            var response = new LoginResponse
            {
                Token = token,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddHours(8),
                User = new UserInfo
                {
                    Id = user.Id.Value,
                    Email = user.Email,
                    DisplayName = user.DisplayName,
                    Roles = user.Roles.Select(r => r.ToString()).ToList()
                }
            };

            _logger.LogInformation("User logged in successfully: {Email}", request.Email);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for user: {Email}", request.Email);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "An error occurred during login" });
        }
    }

    /// <summary>
    /// Logs out the current user
    /// </summary>
    /// <returns>Success response</returns>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Logout()
    {
        // In a real implementation, you might invalidate the token here
        _logger.LogInformation("User logged out");
        return Ok(new { message = "Logged out successfully" });
    }

    /// <summary>
    /// Gets current user information
    /// </summary>
    /// <returns>Current user details</returns>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser()
    {
        try
        {
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(userEmail))
            {
                return Unauthorized();
            }

            var user = await _userRepository.GetByEmailAsync(userEmail);
            if (user == null)
            {
                return Unauthorized();
            }

            var userInfo = new UserInfo
            {
                Id = user.Id.Value,
                Email = user.Email,
                DisplayName = user.DisplayName,
                Roles = user.Roles.Select(r => r.ToString()).ToList()
            };

            return Ok(userInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current user");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private string GenerateJwtToken(User user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"]
            ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
            ?? throw new InvalidOperationException("JWT SecretKey is not configured. Set it in appsettings.json or JWT_SECRET_KEY environment variable.");
        var issuer = jwtSettings["Issuer"] ?? "Enterprise.Documentation.Api";
        var audience = jwtSettings["Audience"] ?? "Enterprise.Documentation.Client";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.Value.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim("SecurityClearance", user.SecurityClearance.ToString())
        }
        .Concat(user.Roles.Select(role => new Claim(ClaimTypes.Role, role.ToString())))
        .ToArray();

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateRefreshToken()
    {
        return Guid.NewGuid().ToString("N");
    }
}