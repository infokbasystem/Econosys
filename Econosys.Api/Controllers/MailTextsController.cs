using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class MailTextsController : ControllerBase
    {
        private const string SubjectType = "SUBJECT";
        private const string BodyType = "BODY";

        private readonly ApplicationDbContext _dbContext;

        public MailTextsController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("search")]
        public async Task<ActionResult<List<MailTextDto>>> Search()
        {
            var rows = await _dbContext.MailTexts
                .AsNoTracking()
                .OrderBy(x => x.Item)
                .ThenBy(x => x.Id)
                .ToListAsync();

            var groups = rows
                .Where(x => !string.IsNullOrWhiteSpace(x.Item))
                .GroupBy(x => new
                {
                    x.CompanyId,
                    Item = NormalizeItemForCompare(x.Item)
                });

            var result = groups
                .Select(group =>
                {
                    var itemRows = group.ToList();
                    var subject = itemRows.FirstOrDefault(IsSubjectTypeRow);
                    var body = itemRows.FirstOrDefault(IsBodyTypeRow);
                    var first = itemRows.First();

                    return MapToDto(
                        subject,
                        body,
                        first,
                        subjectTranslations: null,
                        bodyTranslations: null);
                })
                .OrderBy(x => x.Item)
                .ToList();

            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<MailTextDto>> GetById(int id)
        {
            var seed = await _dbContext.MailTexts
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (seed is null || string.IsNullOrWhiteSpace(seed.Item))
            {
                return NotFound();
            }

            var itemKey = NormalizeItemForCompare(seed.Item);

            var itemRows = await _dbContext.MailTexts
                .AsNoTracking()
                .Where(x => x.CompanyId == seed.CompanyId && x.Item != null && x.Item.ToUpper() == itemKey)
                .OrderBy(x => x.Id)
                .ToListAsync();

            var subject = itemRows.FirstOrDefault(IsSubjectTypeRow);
            var body = itemRows.FirstOrDefault(IsBodyTypeRow);
            var first = itemRows.FirstOrDefault();

            if (first is null)
            {
                return NotFound();
            }

            var subjectTranslations = await EntityTranslationService.BuildCompletedTranslationsAsync(_dbContext, subject?.TranslationCode);
            var bodyTranslations = await EntityTranslationService.BuildCompletedTranslationsAsync(_dbContext, body?.TranslationCode);

            return Ok(MapToDto(subject, body, first, subjectTranslations, bodyTranslations));
        }

        [HttpPost]
        public async Task<ActionResult<MailTextDto>> Create([FromBody] CreateMailTextRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var item = NormalizeItem(request.Item);
            if (string.IsNullOrWhiteSpace(item))
            {
                return BadRequest("Item must be specified.");
            }

            var itemKey = NormalizeItemForCompare(item);

            var companyId = request.CompanyId ?? await ResolveDefaultCompanyIdAsync();
            if (companyId <= 0)
            {
                return BadRequest("CompanyId could not be resolved.");
            }

            var exists = await _dbContext.MailTexts
                .AsNoTracking()
                .AnyAsync(x => x.CompanyId == companyId && x.Item != null && x.Item.ToUpper() == itemKey);

            if (exists)
            {
                return Conflict("MailText item already exists.");
            }

            var subject = new MailText
            {
                CompanyId = companyId,
                Item = item,
                Type = SubjectType
            };

            var body = new MailText
            {
                CompanyId = companyId,
                Item = item,
                Type = BodyType
            };

            _dbContext.MailTexts.Add(subject);
            _dbContext.MailTexts.Add(body);
            await _dbContext.SaveChangesAsync();

            subject.TranslationCode = await EntityTranslationService.UpsertTranslationsAsync(_dbContext, subject.TranslationCode, request.SubjectTranslations);
            body.TranslationCode = await EntityTranslationService.UpsertTranslationsAsync(_dbContext, body.TranslationCode, request.BodyTranslations);

            await _dbContext.SaveChangesAsync();

            var subjectTranslations = await EntityTranslationService.BuildCompletedTranslationsAsync(_dbContext, subject.TranslationCode);
            var bodyTranslations = await EntityTranslationService.BuildCompletedTranslationsAsync(_dbContext, body.TranslationCode);

            var dto = MapToDto(subject, body, subject, subjectTranslations, bodyTranslations);
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<MailTextDto>> Update(int id, [FromBody] UpdateMailTextRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var seed = await _dbContext.MailTexts
                .FirstOrDefaultAsync(x => x.Id == id);

            if (seed is null || string.IsNullOrWhiteSpace(seed.Item))
            {
                return NotFound();
            }

            var targetItem = string.IsNullOrWhiteSpace(request.Item) ? seed.Item!.Trim() : NormalizeItem(request.Item);
            var sourceItemKey = NormalizeItemForCompare(seed.Item);

            var itemRows = await _dbContext.MailTexts
                .Where(x => x.CompanyId == seed.CompanyId && x.Item != null && x.Item.ToUpper() == sourceItemKey)
                .OrderBy(x => x.Id)
                .ToListAsync();

            var subject = itemRows.FirstOrDefault(IsSubjectTypeRow);
            var body = itemRows.FirstOrDefault(IsBodyTypeRow);

            if (subject is null)
            {
                subject = new MailText
                {
                    CompanyId = seed.CompanyId,
                    Item = targetItem,
                    Type = SubjectType
                };
                _dbContext.MailTexts.Add(subject);
            }

            if (body is null)
            {
                body = new MailText
                {
                    CompanyId = seed.CompanyId,
                    Item = targetItem,
                    Type = BodyType
                };
                _dbContext.MailTexts.Add(body);
            }

            subject.Item = targetItem;
            body.Item = targetItem;

            subject.TranslationCode = await EntityTranslationService.UpsertTranslationsAsync(_dbContext, subject.TranslationCode, request.SubjectTranslations);
            body.TranslationCode = await EntityTranslationService.UpsertTranslationsAsync(_dbContext, body.TranslationCode, request.BodyTranslations);

            await _dbContext.SaveChangesAsync();

            var subjectTranslations = await EntityTranslationService.BuildCompletedTranslationsAsync(_dbContext, subject.TranslationCode);
            var bodyTranslations = await EntityTranslationService.BuildCompletedTranslationsAsync(_dbContext, body.TranslationCode);

            return Ok(MapToDto(subject, body, subject, subjectTranslations, bodyTranslations));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var seed = await _dbContext.MailTexts
                .FirstOrDefaultAsync(x => x.Id == id);

            if (seed is null || string.IsNullOrWhiteSpace(seed.Item))
            {
                return NotFound();
            }

            var itemKey = NormalizeItemForCompare(seed.Item);

            var rows = await _dbContext.MailTexts
                .Where(x => x.CompanyId == seed.CompanyId && x.Item != null && x.Item.ToUpper() == itemKey)
                .ToListAsync();

            if (rows.Count == 0)
            {
                return NotFound();
            }

            _dbContext.MailTexts.RemoveRange(rows);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        private static MailTextDto MapToDto(
            MailText? subject,
            MailText? body,
            MailText first,
            List<EntityTranslationDto>? subjectTranslations,
            List<EntityTranslationDto>? bodyTranslations)
        {
            return new MailTextDto
            {
                Id = subject?.Id ?? body?.Id ?? first.Id,
                SubjectId = subject?.Id,
                BodyId = body?.Id,
                CompanyId = first.CompanyId,
                Item = first.Item ?? string.Empty,
                SubjectType = subject?.Type ?? SubjectType,
                BodyType = body?.Type ?? BodyType,
                SubjectTranslationCode = subject?.TranslationCode,
                BodyTranslationCode = body?.TranslationCode,
                SubjectTranslations = subjectTranslations ?? new List<EntityTranslationDto>(),
                BodyTranslations = bodyTranslations ?? new List<EntityTranslationDto>()
            };
        }

        private static bool IsSubjectTypeRow(MailText row)
        {
            var normalized = NormalizeType(row.Type);
            return normalized == SubjectType || normalized.Contains("SUBJECT") || normalized.Contains("AMNE");
        }

        private static bool IsBodyTypeRow(MailText row)
        {
            var normalized = NormalizeType(row.Type);
            return normalized == BodyType || normalized.Contains("BODY") || normalized.Contains("BRODTEXT");
        }

        private static string NormalizeItem(string? item)
        {
            return string.IsNullOrWhiteSpace(item)
                ? string.Empty
                : item.Trim();
        }

        private static string NormalizeItemForCompare(string? item)
        {
            return NormalizeItem(item).ToUpperInvariant();
        }

        private static string NormalizeType(string? type)
        {
            return string.IsNullOrWhiteSpace(type)
                ? string.Empty
                : type.Trim().ToUpperInvariant();
        }

        private async Task<int> ResolveDefaultCompanyIdAsync()
        {
            var fromMailTexts = await _dbContext.MailTexts
                .AsNoTracking()
                .OrderBy(x => x.CompanyId)
                .Select(x => (int?)x.CompanyId)
                .FirstOrDefaultAsync();

            if (fromMailTexts.HasValue)
            {
                return fromMailTexts.Value;
            }

            var fromCompanyInfo = await _dbContext.CompanyInfos
                .AsNoTracking()
                .OrderBy(x => x.CompanyId)
                .Select(x => (int?)x.CompanyId)
                .FirstOrDefaultAsync();

            if (fromCompanyInfo.HasValue)
            {
                return fromCompanyInfo.Value;
            }

            var fromLanguage = await _dbContext.Languages
                .AsNoTracking()
                .OrderBy(x => x.CompanyId)
                .Select(x => (int?)x.CompanyId)
                .FirstOrDefaultAsync();

            return fromLanguage ?? 0;
        }
    }
}
