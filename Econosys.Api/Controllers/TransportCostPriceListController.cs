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
    public class TransportCostPriceListController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public TransportCostPriceListController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<ActionResult<List<TransportCostPriceListOptionDto>>> GetOptions()
        {
            var priceLists = await _dbContext.TransportCostPriceLists
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new TransportCostPriceListOptionDto
                {
                    Id = x.Id,
                    Name = x.Name ?? string.Empty,
                })
                .ToListAsync();

            return Ok(priceLists);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<TransportCostPriceListDto>> GetById(int id)
        {
            var priceList = await _dbContext.TransportCostPriceLists
                .AsNoTracking()
                .Include(x => x.TransportCostPriceListData)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (priceList is null)
            {
                return NotFound();
            }

            return Ok(MapToDto(priceList));
        }

        private static TransportCostPriceListDto MapToDto(Models.TransportCostPriceList priceList)
        {
            return new TransportCostPriceListDto
            {
                Id = priceList.Id,
                Name = priceList.Name,
                ImportName = priceList.ImportName,
                SecaMarpolPallet = priceList.SecaMarpolPallet,
                DmtPercentPallet = priceList.DmtPercentPallet,
                AdditionCurrencyPercentPallet = priceList.AdditionCurrencyPercentPallet,
                OtherPallet = priceList.OtherPallet,
                OtherPercentPallet = priceList.OtherPercentPallet,
                AdditionTotalPercentPallet = priceList.AdditionTotalPercentPallet,
                CurrencyId = priceList.CurrencyId,
                SecaMarpolFtl = priceList.SecaMarpolFtl,
                DmtPercentFtl = priceList.DmtPercentFtl,
                AdditionCurrencyPercentFtl = priceList.AdditionCurrencyPercentFtl,
                OtherFtl = priceList.OtherFtl,
                OtherPercentFtl = priceList.OtherPercentFtl,
                AdditionTotalPercentFtl = priceList.AdditionTotalPercentFtl,
                IsStafflad = priceList.IsStafflad,
                LastImportDateTime = priceList.LastImportDateTime,
                LoadingCost = priceList.LoadingCost,
                UnloadingCost = priceList.UnloadingCost,
                PreCalcAdditionPercent = priceList.PreCalcAdditionPercent,
                PriceListData = priceList.TransportCostPriceListData.Select(MapToDto).ToList(),
            };
        }

        private static TransportCostPriceListDataDto MapToDto(Models.TransportCostPriceListData data)
        {
            return new TransportCostPriceListDataDto
            {
                Id = data.Id,
                TransportCostPriceListId = data.TransportCostPriceListId,
                CountryCode = data.CountryCode,
                PostalNrFrom = data.PostalNrFrom,
                PostalNrTo = data.PostalNrTo,
                Transhipment = data.Transhipment,
                AdditionSekPerPallet = data.AdditionSekPerPallet,
                P1 = data.P1,
                P2 = data.P2,
                P3 = data.P3,
                P4 = data.P4,
                P5 = data.P5,
                P6 = data.P6,
                P7 = data.P7,
                P8 = data.P8,
                P9 = data.P9,
                P10 = data.P10,
                P11 = data.P11,
                P12 = data.P12,
                P13 = data.P13,
                P14 = data.P14,
                P15 = data.P15,
                P16 = data.P16,
                P17 = data.P17,
                P18 = data.P18,
                P19 = data.P19,
                P20 = data.P20,
                P21 = data.P21,
                P22 = data.P22,
                P23 = data.P23,
                P24 = data.P24,
                P25 = data.P25,
                P26 = data.P26,
                P27 = data.P27,
                P28 = data.P28,
                P29 = data.P29,
                P30 = data.P30,
                P31 = data.P31,
                P32 = data.P32,
                PFTL = data.PFTL,
            };
        }
    }
}
