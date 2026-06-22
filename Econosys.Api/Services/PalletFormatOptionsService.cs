using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Services
{
    public class PalletFormatOptionsService : IPalletFormatOptionsService
    {
        private readonly ApplicationDbContext _dbContext;

        public PalletFormatOptionsService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<PalletFormatDto>> GetOptionsAsync(int? customerId, int? supplierId)
        {
            if (!customerId.HasValue && !supplierId.HasValue)
            {
                return new List<PalletFormatDto>();
            }

            var items = _dbContext.PalletFormats
                .AsNoTracking()
                .Select(x => new
                {
                    PalletFormat = x,
                    SupplierPrice = supplierId.HasValue
                        ? x.PalletFormatPrices
                            .Where(price => price.SupplierId == supplierId.Value)
                            .Select(price => price.PalletPrice)
                            .FirstOrDefault()
                        : null,
                    CustomerPrice = customerId.HasValue
                        ? x.PalletFormatPrices
                            .Where(price => price.CustomerId == customerId.Value)
                            .Select(price => price.PalletPrice)
                            .FirstOrDefault()
                        : null,
                });

            if (customerId.HasValue)
            {
                var customerIdValue = customerId.Value;
                items = items.Where(x => x.PalletFormat.PalletFormatPrices.Any(price => price.CustomerId == customerIdValue));
            }

            if (supplierId.HasValue)
            {
                var supplierIdValue = supplierId.Value;
                items = items.Where(x => x.PalletFormat.PalletFormatPrices.Any(price => price.SupplierId == supplierIdValue));
            }

            return await items
                .OrderBy(x => x.PalletFormat.Name)
                .Select(x => new PalletFormatDto
                {
                    Id = x.PalletFormat.Id,
                    Name = x.PalletFormat.Name,
                    M2 = x.PalletFormat.M2,
                    SupplierPrice = x.SupplierPrice,
                    CustomerPrice = x.CustomerPrice,
                    Active = x.PalletFormat.Active,
                    OldDbId = x.PalletFormat.OldDbId,
                    Width = x.PalletFormat.Width,
                    Height = x.PalletFormat.Height,
                    PalletTypeId = x.PalletFormat.PalletTypeId,
                    DebitFactor = x.PalletFormat.DebitFactor,
                    TranslationCode = x.PalletFormat.TranslationCode,
                    SortNr = x.PalletFormat.SortNr,
                    CopyTo = x.PalletFormat.CopyTo,
                })
                .ToListAsync();
        }
    }
}