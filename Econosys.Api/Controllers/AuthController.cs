using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using Econosys.Api.DTOs;
using Econosys.Api.Services;

namespace Econosys.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private const string AccessTokenCookieName = "access_token";
        private readonly IAuthenticationService _authService;
        private readonly ILogger<AuthController> _logger;
        private readonly IConfiguration _configuration;

        public AuthController(
            IAuthenticationService authService,
            ILogger<AuthController> logger,
            IConfiguration configuration)
        {
            _authService = authService;
            _logger = logger;
            _configuration = configuration;
        }


        /// <summary>
        /// Register a new user
        /// </summary>
        /// <param name="request">Registration details</param>
        /// <returns>JWT token if successful</returns>
        [HttpPost("register")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            _logger.LogInformation("Registration attempt received");
            var response = await _authService.RegisterAsync(request);

            if (!response.Success)
                return BadRequest(response);

            SetAuthCookie(response.Token);
            response.Token = null;
            return Ok(response);
        }


        /// <summary>
        /// Login with email and password
        /// </summary>
        /// <param name="request">Login credentials</param>
        /// <returns>JWT token if successful</returns>
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.LoginAsync(request);

            if (!result.Success)
                return Unauthorized(new { message = "Vänligen kontrollera dina inloggningsuppgifter och försök igen." });

            // Set the JWT token as an HttpOnly cookie
            SetAuthCookie(result.Token);

            return Ok(new
            {
                user = result.User
            });
        }


        [HttpPost("logout")]
        [Authorize]
        public IActionResult Logout()
        {
            var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
            
            // Clear the cookie
            Response.Cookies.Delete("access_token", new CookieOptions
            {
                Path = "/",
                SameSite = isDevelopment ? SameSiteMode.Lax : SameSiteMode.None,
                Secure = !isDevelopment
            });

            return Ok(new { message = "Logged out successfully" });
        }


        /// <summary>
        /// Get current authenticated user
        /// </summary>
        /// <returns>Current user details</returns>
        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<UserDto>> GetCurrentUser()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            try
            {
                var user = await _authService.GetUserByIdAsync(userId);
                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving current user for user ID: {UserId}", userId);
                return StatusCode(500, "An error occurred while retrieving user information");
            }
        }


        /// <summary>
        /// Change current user's password
        /// </summary>
        /// <param name="request">Current and new password</param>
        /// <returns>Success message</returns>
        [HttpPost("change-password")]
        [Authorize]
        public async Task<ActionResult<AuthResponse>> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var response = await _authService.ChangePasswordAsync(userId, request);

            if (!response.Success)
                return BadRequest(response);

            return Ok(response);
        }

        private void SetAuthCookie(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return;

            var expirationMinutes = _configuration.GetValue<int>("Jwt:ExpirationMinutes", 60);
            var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

            var options = new CookieOptions
            {
                HttpOnly = true,
                Secure = !isDevelopment,  // false in development (HTTP), true in production (HTTPS)
                SameSite = isDevelopment ? SameSiteMode.Lax : SameSiteMode.None,  // Lax for dev, None for prod
                Expires = DateTimeOffset.UtcNow.AddMinutes(expirationMinutes),
                Path = "/"
            };

            Response.Cookies.Append(AccessTokenCookieName, token, options);
        }
    }
}
