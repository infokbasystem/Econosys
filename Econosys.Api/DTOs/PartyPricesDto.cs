using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs;

/// <summary>A customer or supplier price for a pallet format or extra cost.</summary>
public sealed record PartyPriceDto
{
    [Range(0, int.MaxValue)]
    public int Id { get; init; }

    [Range(1, int.MaxValue)]
    public int ItemId { get; init; }

    [Range(typeof(decimal), "-99999999.99", "99999999.99")]
    public decimal? Price { get; init; }
}

/// <summary>The complete set of pallet and extra-cost prices for one party.</summary>
public sealed record UpdatePartyPricesRequest
{
    [Required]
    public required List<PartyPriceDto> PalletPrices { get; init; }

    [Required]
    public required List<PartyPriceDto> CostPrices { get; init; }
}

/// <summary>Prices and available pallet formats and extra costs.</summary>
public sealed record PartyPricesResponse(
    List<PartyPriceDto> PalletPrices,
    List<PartyPriceDto> CostPrices,
    List<FilterOptionDto<int>> PalletFormats,
    List<FilterOptionDto<int>> Costs);
