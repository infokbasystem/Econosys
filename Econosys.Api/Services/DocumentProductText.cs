using Econosys.Api.Models;
using Econosys.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Services;

public static class DocumentProductText
{
    public sealed record Snapshot(
        string? Product, string? Material, string? MaterialThickness, string? Construction,
        string? Format, string? Color, string? Varnish, string? PrintMethod,
        string? ProductNoteInternal, string? ProductNoteExternal);

    public static Snapshot Build(Product product, IReadOnlyDictionary<int, string?> translations)
    {
        var varnish = product.VarnishId != 7
            ? Translate(product.Varnish?.TranslationCode, product.Varnish?.Name, translations)
            : null;
        if (!string.IsNullOrWhiteSpace(product.VarnishOther))
        {
            varnish = string.IsNullOrWhiteSpace(varnish)
                ? product.VarnishOther
                : $"{varnish}, {product.VarnishOther}";
        }

        var methods = new[]
        {
            product.InternalInfo?.IsOffset == true ? "Offset" : null,
            product.InternalInfo?.IsDigital == true ? "Digital" : null,
            product.InternalInfo?.IsFlexo == true ? "Flexo" : null,
        };
        return new Snapshot(
            product.Name,
            Translate(product.Material?.TranslationCode, product.Material?.Name, translations),
            product.MaterialThickness,
            Translate(product.Construction?.TranslationCode, product.Construction?.Name, translations),
            Format(product), product.NrOfColors, varnish,
            string.Join(", ", methods.Where(value => value != null)),
            product.NoteInternal, product.NoteExternal);
    }

    public static string? ColorWithVarnish(string? color, string? varnish) =>
        string.Join(" + ", new[] { color, varnish }.Where(value => !string.IsNullOrWhiteSpace(value)));

    public static async Task<IReadOnlyDictionary<int, string?>> LoadTranslationsAsync(
        ApplicationDbContext db, Product product, int? languageId, CancellationToken cancellationToken)
    {
        if (!languageId.HasValue)
        {
            return new Dictionary<int, string?>();
        }

        var languageCode = await db.Languages.AsNoTracking().Where(item => item.Id == languageId)
            .Select(item => item.LanguageCode).FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return new Dictionary<int, string?>();
        }

        var codes = new[] { product.Material?.TranslationCode, product.Construction?.TranslationCode, product.Varnish?.TranslationCode };
        var translations = await db.TranslationItems.AsNoTracking()
            .Where(item => item.LangCode == languageCode && codes.Contains(item.TranslationCode))
            .OrderBy(item => item.Id).ToListAsync(cancellationToken);
        return translations.Where(item => item.TranslationCode.HasValue)
            .GroupBy(item => item.TranslationCode!.Value)
            .ToDictionary(group => group.Key, group => group.First().Translation);
    }

    public static string? Translate(int? code, string? fallback, IReadOnlyDictionary<int, string?> translations) =>
        code.HasValue && translations.TryGetValue(code.Value, out var value) ? value : fallback;

    public static string Format(Product? product) =>
        !string.IsNullOrWhiteSpace(product?.Format) ? product.Format
            : string.Join(" x ", new[] { product?.LengthMm, product?.WidthMm, product?.HeightMm }
                .Where(value => value.HasValue && value.Value != 0));

}
