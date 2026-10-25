using Econosys.Api.Models;

namespace Econosys.Api.Services;

public static class DocumentProductText
{
    public static string? Translate(int? code, string? fallback, IReadOnlyDictionary<int, string?> translations) =>
        code.HasValue && translations.TryGetValue(code.Value, out var value) ? value : fallback;

    public static string Format(Product? product) =>
        !string.IsNullOrWhiteSpace(product?.Format) ? product.Format
            : string.Join(" x ", new[] { product?.LengthMm, product?.WidthMm, product?.HeightMm }
                .Where(value => value.HasValue && value.Value != 0));

    public static string Color(Product? product, IReadOnlyDictionary<int, string?> translations)
    {
        var color = product?.NrOfColors ?? "";
        if (product?.Varnish is { } varnish && product.VarnishId != 7)
        {
            color += (string.IsNullOrWhiteSpace(color) ? "" : " + ")
                + Translate(varnish.TranslationCode, varnish.Name, translations);
        }
        if (!string.IsNullOrWhiteSpace(product?.VarnishOther))
        {
            color += (string.IsNullOrWhiteSpace(color) ? "" : ", ") + product.VarnishOther;
        }
        return color;
    }
}
