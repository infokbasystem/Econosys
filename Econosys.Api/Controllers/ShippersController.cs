using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class ShippersController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<ShippersController> _logger;

        public ShippersController(ApplicationDbContext dbContext, ILogger<ShippersController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ShipperDto>> GetById(int id)
        {
            var shipper = await _dbContext.Shippers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (shipper is null)
            {
                return NotFound();
            }

            return Ok(MapToDto(shipper));
        }

        [HttpGet("search")]
        public async Task<ActionResult<List<ShipperDto>>> Search()
        {
            var items = await _dbContext.Shippers
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Id)
                .ToListAsync();

            return Ok(items.Select(MapToDto).ToList());
        }

        [HttpPost]
        public async Task<ActionResult<ShipperDto>> Create([FromBody] CreateShipperRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var shipper = new Shipper
            {
                Name = request.Name,
                ContactPerson = request.ContactPerson,
                Telephone = request.Telephone,
                Mail = request.Mail
            };

            _dbContext.Shippers.Add(shipper);
            await _dbContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = shipper.Id }, MapToDto(shipper));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ShipperDto>> Update(int id, [FromBody] UpdateShipperRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var shipper = await _dbContext.Shippers.FirstOrDefaultAsync(x => x.Id == id);
            if (shipper is null)
            {
                return NotFound();
            }

            shipper.Name = request.Name;
            shipper.ContactPerson = request.ContactPerson;
            shipper.Telephone = request.Telephone;
            shipper.Mail = request.Mail;

            await _dbContext.SaveChangesAsync();

            return Ok(MapToDto(shipper));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var shipper = await _dbContext.Shippers.FirstOrDefaultAsync(x => x.Id == id);
            if (shipper is null)
            {
                return NotFound();
            }

            _dbContext.Shippers.Remove(shipper);

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Failed to delete shipper {ShipperId}", id);
                return Conflict(new { message = "Kunde inte radera transportoren eftersom den anvands av andra poster." });
            }

            return NoContent();
        }

        private static ShipperDto MapToDto(Shipper shipper)
        {
            return new ShipperDto
            {
                Id = shipper.Id,
                Name = shipper.Name,
                ContactPerson = shipper.ContactPerson,
                Telephone = shipper.Telephone,
                Mail = shipper.Mail
            };
        }
    }
}
