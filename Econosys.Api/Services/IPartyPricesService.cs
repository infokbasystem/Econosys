using Econosys.Api.DTOs;

namespace Econosys.Api.Services;

public interface IPartyPricesService
{
    Task<PartyPricesResponse?> GetAsync(int id, bool isSupplier, CancellationToken cancellationToken);
    Task<(bool Found, string? Error)> SaveAsync(
        int id, bool isSupplier, UpdatePartyPricesRequest request, CancellationToken cancellationToken);
}
