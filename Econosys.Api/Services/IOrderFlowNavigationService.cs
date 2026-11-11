using Econosys.Api.DTOs;

namespace Econosys.Api.Services;

public interface IOrderFlowNavigationService
{
    Task<IReadOnlyList<OrderFlowNavigationItemDto>?> GetCalculationFlowAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrderFlowNavigationItemDto>?> GetFlowAsync(string entityType, int id, CancellationToken cancellationToken);
}
