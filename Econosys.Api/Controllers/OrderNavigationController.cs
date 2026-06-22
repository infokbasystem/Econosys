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
    public class OrderNavigationController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public OrderNavigationController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("{entityType}/{id:int}")]
        public async Task<ActionResult<OrderNavigationTreeDto>> GetTree(string entityType, int id)
        {
            var normalizedEntityType = NormalizeEntityType(entityType);
            if (normalizedEntityType is null)
            {
                return BadRequest(new { message = "Unsupported entity type for order navigation." });
            }

            var context = await ResolveContextAsync(normalizedEntityType, id);
            if (context is null)
            {
                return NotFound();
            }

            var calculationIds = await ResolveCalculationIdsAsync(context);

            var tree = new OrderNavigationTreeDto
            {
                ProductId = context.ProductId,
                CurrentEntityType = context.CurrentEntityType,
                CurrentEntityId = context.CurrentEntityId,
            };

            if (calculationIds.Count == 0)
            {
                return Ok(tree);
            }

            var inquiries = await _dbContext.Inquiries
                .AsNoTracking()
                .Where(x => x.CalculationId.HasValue && calculationIds.Contains(x.CalculationId.Value))
                .Select(x => new InquiryNodeProjection
                {
                    Id = x.Id,
                    CalculationId = x.CalculationId,
                })
                .ToListAsync();

            var inquiryIds = inquiries.Select(x => x.Id).ToList();

            var quotations = await _dbContext.Quotations
                .AsNoTracking()
                .Where(x =>
                    (x.CalculationId.HasValue && calculationIds.Contains(x.CalculationId.Value)) ||
                    (x.InquiryId.HasValue && inquiryIds.Contains(x.InquiryId.Value)))
                .Select(x => new QuotationNodeProjection
                {
                    Id = x.Id,
                    CalculationId = x.CalculationId,
                    InquiryId = x.InquiryId,
                })
                .ToListAsync();

            var quotationIds = quotations.Select(x => x.Id).ToList();

            var supplierOrders = await _dbContext.SupplierOrders
                .AsNoTracking()
                .Where(x =>
                    (x.CalculationId.HasValue && calculationIds.Contains(x.CalculationId.Value)) ||
                    (x.InquiryId.HasValue && inquiryIds.Contains(x.InquiryId.Value)) ||
                    (x.QuotationId.HasValue && quotationIds.Contains(x.QuotationId.Value)))
                .Select(x => new SupplierOrderNodeProjection
                {
                    Id = x.Id,
                    CalculationId = x.CalculationId,
                    InquiryId = x.InquiryId,
                    QuotationId = x.QuotationId,
                })
                .ToListAsync();

            var supplierOrderIds = supplierOrders.Select(x => x.Id).ToList();

            var customerOrders = await _dbContext.CustomerOrders
                .AsNoTracking()
                .Where(x =>
                    (x.SupplierOrderId.HasValue && supplierOrderIds.Contains(x.SupplierOrderId.Value)) ||
                    (x.QuotationId.HasValue && quotationIds.Contains(x.QuotationId.Value)))
                .Select(x => new CustomerOrderNodeProjection
                {
                    Id = x.Id,
                    SupplierOrderId = x.SupplierOrderId,
                    QuotationId = x.QuotationId,
                })
                .ToListAsync();

            var customerOrdersBySupplierOrder = customerOrders
                .Where(x => x.SupplierOrderId.HasValue)
                .GroupBy(x => x.SupplierOrderId!.Value)
                .ToDictionary(
                    x => x.Key,
                    x => x
                        .OrderBy(y => y.Id)
                        .Select(y => new OrderNavigationCustomerOrderDto
                        {
                            Id = y.Id,
                            IsCurrent = IsCurrent(context, "customerorder", y.Id),
                        })
                        .ToList());

            var directCustomerOrdersByQuotation = customerOrders
                .Where(x => x.QuotationId.HasValue && !x.SupplierOrderId.HasValue)
                .GroupBy(x => x.QuotationId!.Value)
                .ToDictionary(
                    x => x.Key,
                    x => x
                        .OrderBy(y => y.Id)
                        .Select(y => new OrderNavigationCustomerOrderDto
                        {
                            Id = y.Id,
                            IsCurrent = IsCurrent(context, "customerorder", y.Id),
                        })
                        .ToList());

            var supplierOrdersById = supplierOrders
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .ToDictionary(
                    x => x.Id,
                    x => new OrderNavigationSupplierOrderDto
                    {
                        Id = x.Id,
                        IsCurrent = IsCurrent(context, "supplierorder", x.Id),
                        CustomerOrders = customerOrdersBySupplierOrder.TryGetValue(x.Id, out var orderCustomerOrders)
                            ? orderCustomerOrders
                            : new List<OrderNavigationCustomerOrderDto>(),
                    });

            var supplierOrdersByQuotation = supplierOrders
                .Where(x => x.QuotationId.HasValue)
                .GroupBy(x => x.QuotationId!.Value)
                .ToDictionary(
                    x => x.Key,
                    x => x
                        .OrderBy(y => y.Id)
                        .Select(y => supplierOrdersById[y.Id])
                        .ToList());

            var directSupplierOrdersByInquiry = supplierOrders
                .Where(x => x.InquiryId.HasValue && !x.QuotationId.HasValue)
                .GroupBy(x => x.InquiryId!.Value)
                .ToDictionary(
                    x => x.Key,
                    x => x
                        .OrderBy(y => y.Id)
                        .Select(y => supplierOrdersById[y.Id])
                        .ToList());

            var directSupplierOrdersByCalculation = supplierOrders
                .Where(x => x.CalculationId.HasValue && !x.InquiryId.HasValue && !x.QuotationId.HasValue)
                .GroupBy(x => x.CalculationId!.Value)
                .ToDictionary(
                    x => x.Key,
                    x => x
                        .OrderBy(y => y.Id)
                        .Select(y => supplierOrdersById[y.Id])
                        .ToList());

            var quotationsById = quotations
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .ToDictionary(
                    x => x.Id,
                    x => new OrderNavigationQuotationDto
                    {
                        Id = x.Id,
                        IsCurrent = IsCurrent(context, "quotation", x.Id),
                        SupplierOrders = supplierOrdersByQuotation.TryGetValue(x.Id, out var quotationSupplierOrders)
                            ? quotationSupplierOrders
                            : new List<OrderNavigationSupplierOrderDto>(),
                        CustomerOrders = directCustomerOrdersByQuotation.TryGetValue(x.Id, out var quotationCustomerOrders)
                            ? quotationCustomerOrders
                            : new List<OrderNavigationCustomerOrderDto>(),
                    });

            var quotationsByInquiry = quotations
                .Where(x => x.InquiryId.HasValue)
                .GroupBy(x => x.InquiryId!.Value)
                .ToDictionary(
                    x => x.Key,
                    x => x
                        .OrderBy(y => y.Id)
                        .Select(y => quotationsById[y.Id])
                        .ToList());

            var directQuotationsByCalculation = quotations
                .Where(x => x.CalculationId.HasValue && !x.InquiryId.HasValue)
                .GroupBy(x => x.CalculationId!.Value)
                .ToDictionary(
                    x => x.Key,
                    x => x
                        .OrderBy(y => y.Id)
                        .Select(y => quotationsById[y.Id])
                        .ToList());

            var inquiriesByCalculation = inquiries
                .Where(x => x.CalculationId.HasValue)
                .GroupBy(x => x.CalculationId!.Value)
                .ToDictionary(
                    x => x.Key,
                    x => x
                        .OrderBy(y => y.Id)
                        .Select(y => new OrderNavigationInquiryDto
                        {
                            Id = y.Id,
                            IsCurrent = IsCurrent(context, "inquiry", y.Id),
                            Quotations = quotationsByInquiry.TryGetValue(y.Id, out var inquiryQuotations)
                                ? inquiryQuotations
                                : new List<OrderNavigationQuotationDto>(),
                            SupplierOrders = directSupplierOrdersByInquiry.TryGetValue(y.Id, out var inquirySupplierOrders)
                                ? inquirySupplierOrders
                                : new List<OrderNavigationSupplierOrderDto>(),
                        })
                        .ToList());

            tree.Calculations = calculationIds
                .OrderBy(x => x)
                .Select(calculationId => new OrderNavigationCalculationDto
                {
                    Id = calculationId,
                    IsCurrent = IsCurrent(context, "calculation", calculationId),
                    Inquiries = inquiriesByCalculation.TryGetValue(calculationId, out var calculationInquiries)
                        ? calculationInquiries
                        : new List<OrderNavigationInquiryDto>(),
                    Quotations = directQuotationsByCalculation.TryGetValue(calculationId, out var calculationQuotations)
                        ? calculationQuotations
                        : new List<OrderNavigationQuotationDto>(),
                    SupplierOrders = directSupplierOrdersByCalculation.TryGetValue(calculationId, out var calculationSupplierOrders)
                        ? calculationSupplierOrders
                        : new List<OrderNavigationSupplierOrderDto>(),
                })
                .ToList();

            return Ok(tree);
        }

        private async Task<List<int>> ResolveCalculationIdsAsync(NavigationContext context)
        {
            if (context.ProductId.HasValue)
            {
                return await _dbContext.Calculations
                    .AsNoTracking()
                    .Where(x => x.ProductId == context.ProductId.Value)
                    .OrderBy(x => x.Id)
                    .Select(x => x.Id)
                    .ToListAsync();
            }

            if (context.CalculationId.HasValue)
            {
                return new List<int> { context.CalculationId.Value };
            }

            return new List<int>();
        }

        private async Task<NavigationContext?> ResolveContextAsync(string entityType, int id)
        {
            switch (entityType)
            {
                case "product":
                {
                    var exists = await _dbContext.Products.AsNoTracking().AnyAsync(x => x.Id == id);
                    if (!exists) return null;
                    return new NavigationContext
                    {
                        CurrentEntityType = entityType,
                        CurrentEntityId = id,
                        ProductId = id,
                    };
                }
                case "calculation":
                {
                    var calculation = await _dbContext.Calculations
                        .AsNoTracking()
                        .Where(x => x.Id == id)
                        .Select(x => new { x.Id, x.ProductId })
                        .FirstOrDefaultAsync();

                    if (calculation is null) return null;

                    return new NavigationContext
                    {
                        CurrentEntityType = entityType,
                        CurrentEntityId = id,
                        ProductId = calculation.ProductId,
                        CalculationId = calculation.Id,
                    };
                }
                case "inquiry":
                {
                    var inquiry = await _dbContext.Inquiries
                        .AsNoTracking()
                        .Where(x => x.Id == id)
                        .Select(x => new { x.Id, x.CalculationId })
                        .FirstOrDefaultAsync();

                    if (inquiry is null) return null;

                    int? productId = null;
                    if (inquiry.CalculationId.HasValue)
                    {
                        productId = await _dbContext.Calculations
                            .AsNoTracking()
                            .Where(x => x.Id == inquiry.CalculationId.Value)
                            .Select(x => x.ProductId)
                            .FirstOrDefaultAsync();
                    }

                    return new NavigationContext
                    {
                        CurrentEntityType = entityType,
                        CurrentEntityId = id,
                        ProductId = productId,
                        CalculationId = inquiry.CalculationId,
                    };
                }
                case "quotation":
                {
                    var quotation = await _dbContext.Quotations
                        .AsNoTracking()
                        .Where(x => x.Id == id)
                        .Select(x => new { x.Id, x.CalculationId, x.InquiryId })
                        .FirstOrDefaultAsync();

                    if (quotation is null) return null;

                    var calculationId = quotation.CalculationId;
                    if (!calculationId.HasValue && quotation.InquiryId.HasValue)
                    {
                        calculationId = await _dbContext.Inquiries
                            .AsNoTracking()
                            .Where(x => x.Id == quotation.InquiryId.Value)
                            .Select(x => x.CalculationId)
                            .FirstOrDefaultAsync();
                    }

                    int? productId = null;
                    if (calculationId.HasValue)
                    {
                        productId = await _dbContext.Calculations
                            .AsNoTracking()
                            .Where(x => x.Id == calculationId.Value)
                            .Select(x => x.ProductId)
                            .FirstOrDefaultAsync();
                    }

                    return new NavigationContext
                    {
                        CurrentEntityType = entityType,
                        CurrentEntityId = id,
                        ProductId = productId,
                        CalculationId = calculationId,
                    };
                }
                case "supplierorder":
                {
                    var supplierOrder = await _dbContext.SupplierOrders
                        .AsNoTracking()
                        .Where(x => x.Id == id)
                        .Select(x => new { x.Id, x.CalculationId, x.InquiryId, x.QuotationId })
                        .FirstOrDefaultAsync();

                    if (supplierOrder is null) return null;

                    var calculationId = supplierOrder.CalculationId;

                    if (!calculationId.HasValue && supplierOrder.QuotationId.HasValue)
                    {
                        var quotationCalculationId = await _dbContext.Quotations
                            .AsNoTracking()
                            .Where(x => x.Id == supplierOrder.QuotationId.Value)
                            .Select(x => x.CalculationId)
                            .FirstOrDefaultAsync();

                        calculationId = quotationCalculationId;
                    }

                    if (!calculationId.HasValue && supplierOrder.InquiryId.HasValue)
                    {
                        calculationId = await _dbContext.Inquiries
                            .AsNoTracking()
                            .Where(x => x.Id == supplierOrder.InquiryId.Value)
                            .Select(x => x.CalculationId)
                            .FirstOrDefaultAsync();
                    }

                    int? productId = null;
                    if (calculationId.HasValue)
                    {
                        productId = await _dbContext.Calculations
                            .AsNoTracking()
                            .Where(x => x.Id == calculationId.Value)
                            .Select(x => x.ProductId)
                            .FirstOrDefaultAsync();
                    }

                    return new NavigationContext
                    {
                        CurrentEntityType = entityType,
                        CurrentEntityId = id,
                        ProductId = productId,
                        CalculationId = calculationId,
                    };
                }
                case "customerorder":
                {
                    var customerOrder = await _dbContext.CustomerOrders
                        .AsNoTracking()
                        .Where(x => x.Id == id)
                        .Select(x => new { x.Id, x.CalculationId, x.SupplierOrderId, x.QuotationId })
                        .FirstOrDefaultAsync();

                    if (customerOrder is null) return null;

                    var calculationId = customerOrder.CalculationId;

                    if (!calculationId.HasValue && customerOrder.SupplierOrderId.HasValue)
                    {
                        var supplierOrder = await _dbContext.SupplierOrders
                            .AsNoTracking()
                            .Where(x => x.Id == customerOrder.SupplierOrderId.Value)
                            .Select(x => new { x.CalculationId, x.InquiryId, x.QuotationId })
                            .FirstOrDefaultAsync();

                        calculationId = supplierOrder?.CalculationId;

                        if (!calculationId.HasValue && supplierOrder?.QuotationId is int supplierQuotationId)
                        {
                            calculationId = await _dbContext.Quotations
                                .AsNoTracking()
                                .Where(x => x.Id == supplierQuotationId)
                                .Select(x => x.CalculationId)
                                .FirstOrDefaultAsync();
                        }

                        if (!calculationId.HasValue && supplierOrder?.InquiryId is int supplierInquiryId)
                        {
                            calculationId = await _dbContext.Inquiries
                                .AsNoTracking()
                                .Where(x => x.Id == supplierInquiryId)
                                .Select(x => x.CalculationId)
                                .FirstOrDefaultAsync();
                        }
                    }

                    if (!calculationId.HasValue && customerOrder.QuotationId.HasValue)
                    {
                        var quotation = await _dbContext.Quotations
                            .AsNoTracking()
                            .Where(x => x.Id == customerOrder.QuotationId.Value)
                            .Select(x => new { x.CalculationId, x.InquiryId })
                            .FirstOrDefaultAsync();

                        calculationId = quotation?.CalculationId;

                        if (!calculationId.HasValue && quotation?.InquiryId is int quotationInquiryId)
                        {
                            calculationId = await _dbContext.Inquiries
                                .AsNoTracking()
                                .Where(x => x.Id == quotationInquiryId)
                                .Select(x => x.CalculationId)
                                .FirstOrDefaultAsync();
                        }
                    }

                    int? productId = null;
                    if (calculationId.HasValue)
                    {
                        productId = await _dbContext.Calculations
                            .AsNoTracking()
                            .Where(x => x.Id == calculationId.Value)
                            .Select(x => x.ProductId)
                            .FirstOrDefaultAsync();
                    }

                    return new NavigationContext
                    {
                        CurrentEntityType = entityType,
                        CurrentEntityId = id,
                        ProductId = productId,
                        CalculationId = calculationId,
                    };
                }
                default:
                    return null;
            }
        }

        private static bool IsCurrent(NavigationContext context, string entityType, int id)
        {
            return context.CurrentEntityType == entityType && context.CurrentEntityId == id;
        }

        private static string? NormalizeEntityType(string entityType)
        {
            var normalized = (entityType ?? string.Empty).Trim().ToLowerInvariant().Replace("-", string.Empty).Replace("_", string.Empty);

            return normalized switch
            {
                "product" => "product",
                "calculation" => "calculation",
                "inquiry" => "inquiry",
                "quotation" => "quotation",
                "supplierorder" => "supplierorder",
                "customerorder" => "customerorder",
                _ => null,
            };
        }

        private sealed class NavigationContext
        {
            public string CurrentEntityType { get; set; } = string.Empty;
            public int CurrentEntityId { get; set; }
            public int? ProductId { get; set; }
            public int? CalculationId { get; set; }
        }

        private sealed class InquiryNodeProjection
        {
            public int Id { get; set; }
            public int? CalculationId { get; set; }
        }

        private sealed class QuotationNodeProjection
        {
            public int Id { get; set; }
            public int? CalculationId { get; set; }
            public int? InquiryId { get; set; }
        }

        private sealed class SupplierOrderNodeProjection
        {
            public int Id { get; set; }
            public int? CalculationId { get; set; }
            public int? InquiryId { get; set; }
            public int? QuotationId { get; set; }
        }

        private sealed class CustomerOrderNodeProjection
        {
            public int Id { get; set; }
            public int? SupplierOrderId { get; set; }
            public int? QuotationId { get; set; }
        }
    }
}
