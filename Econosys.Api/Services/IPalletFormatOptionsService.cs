using Econosys.Api.DTOs;

namespace Econosys.Api.Services
{
    public interface IPalletFormatOptionsService
    {
        Task<List<PalletFormatDto>> GetOptionsAsync(int? customerId, int? supplierId);
    }
}