using Microsoft.AspNetCore.Identity;
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
                    CreatedAt = DateTime.UtcNow,
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
                if (user == null || !user.IsActive)
                {
                    return new AuthResponse 
                    { 
                        Success = false, 
                        Message = "Invalid email or password" 
                    };
                }

                var result = await _signInManager.PasswordSignInAsync(
                    user.UserName ?? user.Email ?? request.Email, request.Password, false, lockoutOnFailure: true);

                if (result.IsLockedOut)
                {
                    _logger.LogWarning("Account locked out for user ID: {UserId}", user.Id);
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Account is temporarily locked due to multiple failed attempts. Try again later."
                    };
                }

                if (!result.Succeeded)
                {
                    _logger.LogWarning("Failed login attempt for user ID: {UserId}", user.Id);
                    return new AuthResponse 
                    { 
                        Success = false, 
                        Message = "Invalid email or password" 
                    };
                }

                // Update last login
                user.LastLoginAt = DateTime.UtcNow;
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
                _logger.LogError(ex, "Unexpected error during login");
                return new AuthResponse 
                { 
                    Success = false, 
                    Message = "An error occurred during login" 
                };
            }
        }

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
