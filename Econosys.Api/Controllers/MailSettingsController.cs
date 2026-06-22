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
    public class MailSettingsController : ControllerBase
    {
        private const string ProtectedSettingSendPassword = "SendPassword";

        private readonly ApplicationDbContext _dbContext;

        public MailSettingsController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("{companyId:int}")]
        public async Task<ActionResult<MailSettingsByCompanyDto>> GetByCompanyId(int companyId)
        {
            var rows = await _dbContext.MailSettings
                .AsNoTracking()
                .Where(x => x.CompanyId == companyId)
                .OrderBy(x => x.Id)
                .ToListAsync();

            if (rows.Count == 0)
            {
                return NotFound();
            }

            return Ok(MapToDto(companyId, rows));
        }

        [HttpPut("{companyId:int}")]
        public async Task<ActionResult<MailSettingsByCompanyDto>> Update(int companyId, [FromBody] UpdateMailSettingsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var rows = await _dbContext.MailSettings
                .Where(x => x.CompanyId == companyId)
                .OrderBy(x => x.Id)
                .ToListAsync();

            var managedRows = rows
                .Where(x => !IsProtectedSetting(x.Setting))
                .ToList();

            var incoming = (request.Settings ?? new List<UpsertMailSettingRequest>())
                .Where(x => !string.IsNullOrWhiteSpace(x.Setting))
                .Where(x => !IsProtectedSetting(x.Setting))
                .Select(x => new
                {
                    Id = x.Id,
                    Setting = x.Setting!.Trim(),
                    Value = string.IsNullOrWhiteSpace(x.Value) ? null : x.Value.Trim()
                })
                .ToList();

            var incomingIds = incoming
                .Where(x => x.Id.HasValue)
                .Select(x => x.Id!.Value)
                .ToHashSet();

            var toDelete = managedRows.Where(x => !incomingIds.Contains(x.Id)).ToList();
            if (toDelete.Count > 0)
            {
                _dbContext.MailSettings.RemoveRange(toDelete);
            }

            foreach (var item in incoming)
            {
                if (item.Id.HasValue)
                {
                    var existing = managedRows.FirstOrDefault(x => x.Id == item.Id.Value);
                    if (existing is null)
                    {
                        return BadRequest($"MailSetting with id {item.Id.Value} was not found for company {companyId}.");
                    }

                    existing.Setting = item.Setting;
                    existing.Value = item.Value;
                    existing.MailWrapper = request.MailWrapper;
                    continue;
                }

                _dbContext.MailSettings.Add(new MailSetting
                {
                    CompanyId = companyId,
                    Setting = item.Setting,
                    Value = item.Value,
                    MailWrapper = request.MailWrapper
                });
            }

            // If there are existing rows left that were not in incoming (because incoming was empty),
            // we still synchronize MailWrapper across all company rows.
            var remaining = await _dbContext.MailSettings
                .Where(x => x.CompanyId == companyId)
                .ToListAsync();

            foreach (var row in remaining)
            {
                row.MailWrapper = request.MailWrapper;
            }

            // Allow storing only mail wrapper even when there are no key/value rows.
            if (remaining.Count == 0 && !string.IsNullOrWhiteSpace(request.MailWrapper))
            {
                _dbContext.MailSettings.Add(new MailSetting
                {
                    CompanyId = companyId,
                    Setting = "MAILWRAPPER",
                    Value = null,
                    MailWrapper = request.MailWrapper
                });
            }

            await _dbContext.SaveChangesAsync();

            var updated = await _dbContext.MailSettings
                .AsNoTracking()
                .Where(x => x.CompanyId == companyId)
                .OrderBy(x => x.Id)
                .ToListAsync();

            return Ok(MapToDto(companyId, updated));
        }

        private static MailSettingsByCompanyDto MapToDto(int companyId, List<MailSetting> rows)
        {
            var wrapper = rows
                .Select(x => x.MailWrapper)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

            return new MailSettingsByCompanyDto
            {
                CompanyId = companyId,
                MailWrapper = wrapper,
                Settings = rows
                    .Where(x => !IsProtectedSetting(x.Setting))
                    .Where(x => !string.Equals(x.Setting, "MAILWRAPPER", StringComparison.OrdinalIgnoreCase))
                    .Select(x => new MailSettingDto
                    {
                        Id = x.Id,
                        CompanyId = x.CompanyId,
                        Setting = x.Setting,
                        Value = x.Value,
                        OldDbId = x.OldDbId
                    })
                    .ToList()
            };
        }

        private static bool IsProtectedSetting(string? setting)
        {
            return string.Equals(setting?.Trim(), ProtectedSettingSendPassword, StringComparison.OrdinalIgnoreCase);
        }
    }
}
