using Microsoft.AspNetCore.Identity;
using System.Data.Common;
using System.Net.Sockets;
using Econosys.Api.Data;
using Econosys.Api.DTOs;

namespace Econosys.Api.Services
{
    public interface IAuthenticationService
    {
        Task<AuthResponse> RegisterAsync(RegisterRequest request);
        Task<AuthResponse> LoginAsync(LoginRequest request);
        Task<AuthResponse> ChangePasswordAsync(string userId, ChangePasswordRequest request);
        Task<UserDto> GetUserByIdAsync(string userId);
    }

    public class AuthenticationService : IAuthenticationService
    {
        private const int MaxFailedLoginAttempts = 10;

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly ILogger<AuthenticationService> _logger;

        public AuthenticationService(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IJwtTokenService jwtTokenService,
            ILogger<AuthenticationService> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtTokenService = jwtTokenService;
            _logger = logger;
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            try
            {
                // Check if user already exists
                // NOTE: Do not reveal whether the email exists to prevent user enumeration
                var existingUser = await _userManager.FindByEmailAsync(request.Email);
                if (existingUser != null)
                {
                    return new AuthResponse 
                    { 
                        Success = false, 
                        Message = "Registration failed. Please check the requirements and try again." 
                    };
                }

                // Create new user
                var user = new ApplicationUser
                {
                    UserName = request.Email,
                    Email = request.Email,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    CreatedAt = SwedishTime.Now,
                    IsActive = true
                };

                var result = await _userManager.CreateAsync(user, request.Password);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogWarning("User registration failed: {Errors}", errors);
                    return new AuthResponse 
                    { 
                        Success = false, 
                        Message = "Registration failed. Please check the requirements and try again." 
                    };
                }

                // Assign default role
                await _userManager.AddToRoleAsync(user, "User");

                // Generate token
                var roles = await _userManager.GetRolesAsync(user);
                var token = _jwtTokenService.GenerateAccessToken(user.Id, user.Email, roles.ToList());

                return new AuthResponse
                {
                    Success = true,
                    Message = "User registered successfully",
                    Token = token,
                    User = MapToUserDto(user, roles.ToList())
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during registration");
                return new AuthResponse 
                { 
                    Success = false, 
                    Message = "An error occurred during registration" 
                };
            }
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                {
                    return new AuthResponse 
                    { 
                        Success = false, 
                        Message = "Invalid email or password" 
                    };
                }

                if (await _userManager.IsLockedOutAsync(user))
                {
                    _logger.LogWarning("Login attempt on locked out account, user ID: {UserId}", user.Id);
                    return BuildLockedOutResponse();
                }

                if (!user.IsActive)
                {
                    return new AuthResponse 
                    { 
                        Success = false, 
                        Message = "Invalid email or password" 
                    };
                }

                if (!await _userManager.CheckPasswordAsync(user, request.Password))
                {
                    var lockedOut = await RegisterFailedLoginAttemptAsync(user);
                    if (lockedOut)
                        return BuildLockedOutResponse();

                    return new AuthResponse 
                    { 
                        Success = false, 
                        Message = "Invalid email or password" 
                    };
                }

                // Update last login and clear failed attempts
                user.AccessFailedCount = 0;
                user.LastLoginAt = SwedishTime.Now;
                await _userManager.UpdateAsync(user);

                // Generate token
                var roles = await _userManager.GetRolesAsync(user);
                var token = _jwtTokenService.GenerateAccessToken(user.Id, user.Email ?? request.Email, roles.ToList());

                return new AuthResponse
                {
                    Success = true,
                    Message = "Login successful",
                    Token = token,
                    User = MapToUserDto(user, roles.ToList())
                };
            }
            catch (Exception ex)
            {
                if (IsDatabaseUnavailable(ex))
                {
                    _logger.LogError(ex, "Database unavailable during login");
                    return new AuthResponse
                    {
                        Success = false,
                        IsDatabaseUnavailable = true,
                        Message = "Kan inte ansluta till databasen. Kontakta administratören."
                    };
                }

                _logger.LogError(ex, "Unexpected error during login");
                return new AuthResponse 
                { 
                    Success = false, 
                    Message = "An error occurred during login" 
                };
            }
        }

        private static bool IsDatabaseUnavailable(Exception exception)
        {
            for (Exception? ex = exception; ex != null; ex = ex.InnerException)
            {
                if (ex is DbException || ex is SocketException || ex is TimeoutException)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Increments AccessFailedCount and locks the account permanently when the limit is reached.
        /// </summary>
        /// <returns>True if the account is now locked out.</returns>
        private async Task<bool> RegisterFailedLoginAttemptAsync(ApplicationUser user)
        {
            user.LockoutEnabled = true;
            user.AccessFailedCount++;

            var lockedOut = user.AccessFailedCount >= MaxFailedLoginAttempts;
            if (lockedOut)
                user.LockoutEnd = DateTimeOffset.MaxValue;

            await _userManager.UpdateAsync(user);

            if (lockedOut)
                _logger.LogWarning("Account locked out after {Attempts} failed attempts, user ID: {UserId}", user.AccessFailedCount, user.Id);
            else
                _logger.LogWarning("Failed login attempt {Attempts}/{Max} for user ID: {UserId}", user.AccessFailedCount, MaxFailedLoginAttempts, user.Id);

            return lockedOut;
        }

        private static AuthResponse BuildLockedOutResponse() => new()
        {
            Success = false,
            IsLockedOut = true,
            Message = "Ditt konto är låst på grund av för många misslyckade inloggningsförsök. Kontakta administratören för att låsa upp kontot."
        };

        public async Task<AuthResponse> ChangePasswordAsync(string userId, ChangePasswordRequest request)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return new AuthResponse 
                    { 
                        Success = false, 
                        Message = "User not found" 
                    };
                }

                var result = await _userManager.ChangePasswordAsync(
                    user, request.CurrentPassword, request.NewPassword);

                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return new AuthResponse 
                    { 
                        Success = false, 
                        Message = $"Password change failed: {errors}" 
                    };
                }

                return new AuthResponse
                {
                    Success = true,
                    Message = "Password changed successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while changing password");
                return new AuthResponse 
                { 
                    Success = false, 
                    Message = "An error occurred while changing password" 
                };
            }
        }

        public async Task<UserDto> GetUserByIdAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new InvalidOperationException("User not found");

            var roles = await _userManager.GetRolesAsync(user);
            return MapToUserDto(user, roles.ToList());
        }

        private UserDto MapToUserDto(ApplicationUser user, List<string> roles)
        {
            return new UserDto
            {
                Id = user.Id,
                Email = user.Email ?? "",
                FirstName = user.FirstName ?? "",
                LastName = user.LastName ?? "",
                CreatedAt = user.CreatedAt,
                Roles = roles
            };
        }
    }
}
