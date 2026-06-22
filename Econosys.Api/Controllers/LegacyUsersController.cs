using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class LegacyUsersController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public LegacyUsersController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("active-options")]
        public async Task<ActionResult<List<FilterOptionDto<string>>>> GetActiveOptions()
        {
            var users = await _dbContext.LegacyUsers
                .AsNoTracking()
                .Where(x => x.Active && x.Name != null && x.Name != "")
                .OrderBy(x => x.Name)
                .Select(x => new FilterOptionDto<string>
                {
                    Id = x.Name!,
                    Name = x.Name!,
                    IsActive = x.Active
                })
                .ToListAsync();

            return Ok(users);
        }
    }
}