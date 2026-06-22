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
    public class ImprovementPropositionsController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public ImprovementPropositionsController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ImprovementPropositionDto>> GetById(int id)
        {
            var entity = await _dbContext.ImprovementPropositions
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            return Ok(MapToDto(entity));
        }

        [HttpPost]
        public async Task<ActionResult<ImprovementPropositionDto>> Create([FromBody] CreateImprovementPropositionRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var entity = new ImprovementProposition
            {
                CreatedByName = request.CreatedByName,
                CreatedTimestamp = request.CreatedTimestamp ?? SwedishTime.Now,
                Description = request.Description,
                ProposedMeasure = request.ProposedMeasure,
                Responsible = request.Responsible,
                AreaCode = request.AreaCode,
                StatusCode = request.StatusCode,
                Note = request.Note,
                FollowUp = request.FollowUp
            };

            _dbContext.ImprovementPropositions.Add(entity);
            await _dbContext.SaveChangesAsync();

            var response = MapToDto(entity);
            return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ImprovementPropositionDto>> Update(int id, [FromBody] UpdateImprovementPropositionRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var entity = await _dbContext.ImprovementPropositions.FirstOrDefaultAsync(x => x.Id == id);
            if (entity is null)
            {
                return NotFound();
            }

            entity.CreatedByName = request.CreatedByName ?? string.Empty;
            entity.CreatedTimestamp = request.CreatedTimestamp ?? SwedishTime.Now;
            entity.Description = request.Description ?? string.Empty;
            entity.ProposedMeasure = request.ProposedMeasure ?? string.Empty;
            entity.Responsible = request.Responsible ?? string.Empty;
            entity.AreaCode = request.AreaCode ?? ImprovementPropositionAreaCode.SALES;
            entity.StatusCode = request.StatusCode ?? ImprovementPropositionStatusCode.NEW;
            entity.Note = request.Note;
            entity.FollowUp = request.FollowUp;

            await _dbContext.SaveChangesAsync();

            return Ok(MapToDto(entity));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _dbContext.ImprovementPropositions.FirstOrDefaultAsync(x => x.Id == id);
            if (entity is null)
            {
                return NotFound();
            }

            _dbContext.ImprovementPropositions.Remove(entity);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("search")]
        public async Task<ActionResult<PagedResultDto<ImprovementPropositionDto>>> Search([FromBody] SearchImprovementPropositionsRequest? request)
        {
            IQueryable<ImprovementProposition> query = _dbContext.ImprovementPropositions.AsNoTracking();
            var pagination = request?.Pagination;

            if (!string.IsNullOrWhiteSpace(request?.SearchTerm))
            {
                var searchTerm = request.SearchTerm.Trim().ToLower();
                query = query.Where(x =>
                    x.CreatedByName.ToLower().Contains(searchTerm) ||
                    x.Description.ToLower().Contains(searchTerm) ||
                    x.ProposedMeasure.ToLower().Contains(searchTerm) ||
                    x.Responsible.ToLower().Contains(searchTerm) ||
                    (x.Note != null && x.Note.ToLower().Contains(searchTerm)) ||
                    (x.FollowUp != null && x.FollowUp.ToLower().Contains(searchTerm)));
            }

            if (request?.StatusCode.HasValue == true)
            {
                query = query.Where(x => x.StatusCode == request.StatusCode.Value);
            }

            if (request?.AreaCode.HasValue == true)
            {
                query = query.Where(x => x.AreaCode == request.AreaCode.Value);
            }

            if (!string.IsNullOrWhiteSpace(request?.Responsible))
            {
                var responsible = request.Responsible.Trim();
                query = query.Where(x => x.Responsible == responsible);
            }

            if (request?.CreatedFrom.HasValue == true)
            {
                query = query.Where(x => x.CreatedTimestamp >= request.CreatedFrom.Value);
            }

            if (request?.CreatedTo.HasValue == true)
            {
                query = query.Where(x => x.CreatedTimestamp <= request.CreatedTo.Value);
            }

            query = (request?.SortBy?.Trim().ToLowerInvariant(), request?.SortDescending ?? false) switch
            {
                ("createdbyname", true) => query.OrderByDescending(x => x.CreatedByName),
                ("createdbyname", false) => query.OrderBy(x => x.CreatedByName),
                ("createdtimestamp", true) => query.OrderByDescending(x => x.CreatedTimestamp),
                ("createdtimestamp", false) => query.OrderBy(x => x.CreatedTimestamp),
                ("responsible", true) => query.OrderByDescending(x => x.Responsible),
                ("responsible", false) => query.OrderBy(x => x.Responsible),
                ("areacode", true) => query.OrderByDescending(x => x.AreaCode),
                ("areacode", false) => query.OrderBy(x => x.AreaCode),
                ("statuscode", true) => query.OrderByDescending(x => x.StatusCode),
                ("statuscode", false) => query.OrderBy(x => x.StatusCode),
                (_, true) => query.OrderByDescending(x => x.Id),
                _ => query.OrderBy(x => x.Id)
            };

            var totalCount = await query.CountAsync();
            List<ImprovementProposition> items;
            int pageNumber;
            int pageSize;
            int totalPages;

            if (pagination is null)
            {
                items = await query.ToListAsync();
                pageNumber = 1;
                pageSize = totalCount;
                totalPages = totalCount == 0 ? 0 : 1;
            }
            else
            {
                items = await query
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .ToListAsync();

                pageNumber = pagination.PageNumber;
                pageSize = pagination.PageSize;
                totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pagination.PageSize);
            }

            return Ok(new PagedResultDto<ImprovementPropositionDto>
            {
                Items = items.Select(MapToDto).ToList(),
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            });
        }

        private static ImprovementPropositionDto MapToDto(ImprovementProposition entity)
        {
            return new ImprovementPropositionDto
            {
                Id = entity.Id,
                CreatedByName = entity.CreatedByName,
                CreatedTimestamp = entity.CreatedTimestamp,
                Description = entity.Description,
                ProposedMeasure = entity.ProposedMeasure,
                Responsible = entity.Responsible,
                AreaCode = entity.AreaCode,
                StatusCode = entity.StatusCode,
                Note = entity.Note,
                FollowUp = entity.FollowUp
            };
        }
    }
}
