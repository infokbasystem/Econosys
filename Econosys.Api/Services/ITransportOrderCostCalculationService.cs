using Econosys.Api.Data;
using Econosys.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Services
{
    // Result wrapper: v1 deliberately rejects branches not yet ported (LTL, ArchivedAutoFreightCalc) instead of guessing.
    public sealed class TransportOrderCostCalculationResult
    {
        public bool Success { get; private init; }
        public string? ErrorMessage { get; private init; }
        public TransportOrderCostCalc? CostCalc { get; private init; }

        public static TransportOrderCostCalculationResult Ok(TransportOrderCostCalc costCalc) => new() { Success = true, CostCalc = costCalc };
        public static TransportOrderCostCalculationResult Fail(string message) => new() { Success = false, ErrorMessage = message };
    }

    public interface ITransportOrderCostCalculationService
    {
        Task<TransportOrderCostCalculationResult> RecalculateAsync(int transportOrderId, int deliveryStatus);
    }
}
