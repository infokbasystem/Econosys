using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Services
{
    public static class EntityTranslationService
    {
        public static async Task<List<EntityTranslationDto>> BuildCompletedTranslationsAsync(
            ApplicationDbContext dbContext,
            int? translationCode,
            CancellationToken cancellationToken = default)
        {
            var languageCodes = await GetLanguageCodesAsync(dbContext, cancellationToken);

            if (!translationCode.HasValue)
            {
                return languageCodes
                    .Select(code => new EntityTranslationDto
                    {
                        LangCode = code,
                        Translation = string.Empty,
                        TranslationHtml = null
                    })
                    .ToList();
            }

            var existing = await dbContext.TranslationItems
                .AsNoTracking()
                .Where(x => x.TranslationCode == translationCode.Value)
                .ToListAsync(cancellationToken);

            var existingByCode = existing
                .Where(x => !string.IsNullOrWhiteSpace(x.LangCode))
                .GroupBy(x => NormalizeCode(x.LangCode!))
                .ToDictionary(g => g.Key, g => g.First());

            var orderedCodes = languageCodes
                .Concat(existingByCode.Keys.Where(code => !languageCodes.Contains(code, StringComparer.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return orderedCodes
                .Select(code =>
                {
                    existingByCode.TryGetValue(NormalizeCode(code), out var existingItem);
                    return new EntityTranslationDto
                    {
                        LangCode = code,
                        Translation = existingItem?.Translation ?? string.Empty,
                        TranslationHtml = existingItem?.TranslationHtml
                    };
                })
                .ToList();
        }

        public static async Task<int?> UpsertTranslationsAsync(
            ApplicationDbContext dbContext,
            int? translationCode,
            List<EntityTranslationRequest>? translations,
            CancellationToken cancellationToken = default)
        {
            if (translations is null)
            {
                return translationCode;
            }

            var requestedByCode = translations
                .Where(x => !string.IsNullOrWhiteSpace(x.LangCode))
                .GroupBy(x => NormalizeCode(x.LangCode!))
                .ToDictionary(g => g.Key, g => g.Last());

            var languageCodes = await GetLanguageCodesAsync(dbContext, cancellationToken);
            var mergedCodes = languageCodes
                .Concat(requestedByCode.Keys.Where(code => !languageCodes.Contains(code, StringComparer.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (mergedCodes.Count == 0)
            {
                return translationCode;
            }

            if (!translationCode.HasValue)
            {
                translationCode = await GetNextTranslationCodeAsync(dbContext, cancellationToken);
            }

            var existing = await dbContext.TranslationItems
                .Where(x => x.TranslationCode == translationCode.Value)
                .ToListAsync(cancellationToken);

            var existingByCode = existing
                .Where(x => !string.IsNullOrWhiteSpace(x.LangCode))
                .GroupBy(x => NormalizeCode(x.LangCode!))
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var code in mergedCodes)
            {
                requestedByCode.TryGetValue(NormalizeCode(code), out var requested);

                if (existingByCode.TryGetValue(NormalizeCode(code), out var existingItem))
                {
                    if (requested is not null)
                    {
                        existingItem.Translation = requested.Translation ?? string.Empty;
                        existingItem.TranslationHtml = requested.TranslationHtml;
                    }

                    continue;
                }

                dbContext.TranslationItems.Add(new TranslationItem
                {
                    TranslationCode = translationCode.Value,
                    LangCode = code,
                    Translation = requested?.Translation ?? string.Empty,
                    TranslationHtml = requested?.TranslationHtml
                });
            }

            return translationCode;
        }

        private static async Task<List<string>> GetLanguageCodesAsync(
            ApplicationDbContext dbContext,
            CancellationToken cancellationToken)
        {
            return await dbContext.Languages
                .AsNoTracking()
                .Where(x => !string.IsNullOrWhiteSpace(x.LanguageCode))
                .Select(x => x.LanguageCode!.Trim())
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(cancellationToken);
        }

        public static async Task<int> GetNextTranslationCodeAsync(
            ApplicationDbContext dbContext,
            CancellationToken cancellationToken = default)
        {
            var maxCode = await dbContext.TranslationItems
                .AsNoTracking()
                .MaxAsync(x => x.TranslationCode, cancellationToken);

            return (maxCode ?? 0) + 1;
        }

        private static string NormalizeCode(string code)
        {
            return code.Trim().ToUpperInvariant();
        }
    }
}