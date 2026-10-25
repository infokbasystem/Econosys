using Econosys.Api.DTOs;

namespace Econosys.Api.Services;

public interface IOrderFlowNavigationService
{
    Task<IReadOnlyList<OrderFlowNavigationItemDto>?> GetCalculationFlowAsync(int id, CancellationToken cancellationToken);
}
