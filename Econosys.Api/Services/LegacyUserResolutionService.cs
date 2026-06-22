using System.Security.Claims;
using Econosys.Api.Data;
using Econosys.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Services
{
    public interface ILegacyUserResolutionService
    {
        Task<LegacyUser?> ResolveCurrentUserAsync(ClaimsPrincipal user);
    }

    public class LegacyUserResolutionService : ILegacyUserResolutionService
    {
        private readonly ApplicationDbContext _dbContext;

        public LegacyUserResolutionService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<LegacyUser?> ResolveCurrentUserAsync(ClaimsPrincipal user)
        {
            var email = user.FindFirstValue(ClaimTypes.Email)?.Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            var legacyUser = await _dbContext.LegacyUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(lu => lu.Email == email);

            return legacyUser;
        }
    }
}
