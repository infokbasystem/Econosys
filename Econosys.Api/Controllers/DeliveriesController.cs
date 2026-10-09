using System.Globalization;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class DeliveriesController : ControllerBase
    {
        private static readonly string[] AllowedDeliveryTypes =
        {
            "DeliveryToCustomer",
            "DeliveryToStock",
            "DeliveryFromStock"
        };

        private static readonly Dictionary<string, PropertyInfo> FieldMap = BuildFieldMap();

        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<DeliveriesController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public DeliveriesController(
            ApplicationDbContext dbContext,
            ILogger<DeliveriesController> logger,
            IWebHostEnvironment webHostEnvironment)
        {
            _dbContext = dbContext;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpGet("new/order/{customerOrderId:int}")]
        public async Task<ActionResult<NewDeliveryOrderInfoDto>> GetNewDeliveryOrderInfo(int customerOrderId)
        {
            var order = await _dbContext.CustomerOrders.AsNoTracking()
                .Include(x => x.SupplierOrder)
                .Include(x => x.SelectedCalculationRow)
                .FirstOrDefaultAsync(x => x.Id == customerOrderId);

            if (order is null)
            {
                return NotFound();
            }

            var calculationRow = order.SelectedCalculationRow;
            var inventoryIsInventory = order.SupplierOrder?.InventoryId is int inventoryId
                ? await _dbContext.Inventories.AsNoTracking()
                    .Where(x => x.Id == inventoryId)
                    .Select(x => (bool?)x.IsInventory)
                    .FirstOrDefaultAsync()
                : null;

            return Ok(new NewDeliveryOrderInfoDto
            {
                CustomerOrderId = order.Id,
                CustomerOrderNr = order.CustomerOrderNr ?? string.Empty,
                CustomerName = order.CustomerName ?? string.Empty,
                CustomerOrderDeliveryDate = order.DeliveryDate,
                ProductName = order.SupplierOrder?.Product ?? order.Product ?? string.Empty,
                OrderedEdition = order.Edition,
                IsPallet = order.PalletFormatId.HasValue
                    || calculationRow?.ArchivedPalletFormatIdCustomerOrder.HasValue == true,
                CustomerPalletFormatId = order.PalletFormatId,
                SupplierOrderId = order.SupplierOrderId,
                SupplierOrderNr = order.SupplierOrder?.SupplierOrderNr ?? string.Empty,
                ProducedEdition = order.SupplierOrder?.ProducedEdition,
                InventoryId = order.SupplierOrder?.InventoryId,
                InventoryIsInventory = inventoryIsInventory,
                EditionPerPallet = calculationRow?.ArchivedEditionPerPallet,
                PalletCalcFactor = calculationRow?.ArchivedPalletCalcFactor,
                PalletLength = calculationRow?.ArchivedPalletLength,
                PalletWidth = calculationRow?.ArchivedPalletWidth,
                PalletHeight = calculationRow?.ArchivedPalletHeight,
                PalletIsStackable = calculationRow?.ArchivedPalletIsStackable ?? false,
                CalculationAutoFreightCalc = calculationRow?.ArchivedAutoFreightCalc ?? false,
                PackagingType = order.SupplierOrder?.PackagingType ?? string.Empty,
                NrOfPerBundle = calculationRow?.ArchivedNrOfPerBundle,
                NrOfPerOuterPackaging = calculationRow?.ArchivedNrOfPerOuterPackaging,
                CustomerDeliveryAddressId = order.CustomerDeliveryAddressId
                    ?? order.SupplierOrder?.CustomerDeliveryAddressId,
            });
        }

        [HttpGet("new/legs")]
        public async Task<ActionResult<List<CreateNewDeliveryLegRequest>>> GetNewDeliveryLegs(
            [FromQuery] int customerOrderId,
            [FromQuery] int deliveryType,
            [FromQuery] int? inventoryId)
        {
            if (deliveryType is < 1 or > 3)
            {
                return BadRequest("Leveranstypen är ogiltig.");
            }

            var customerOrder = await _dbContext.CustomerOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == customerOrderId);
            if (customerOrder is null)
            {
                return NotFound();
            }

            var supplierOrder = customerOrder.SupplierOrderId.HasValue
                ? await _dbContext.SupplierOrders.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == customerOrder.SupplierOrderId.Value)
                : null;
            var supplierFactoryPositionId = supplierOrder?.SupplierFactoryId.HasValue == true
                ? await _dbContext.SupplierFactories.AsNoTracking()
                    .Where(x => x.Id == supplierOrder.SupplierFactoryId.Value)
                    .Select(x => x.PositionId)
                    .FirstOrDefaultAsync()
                : null;
            var inventoryPositionId = inventoryId.HasValue
                ? await _dbContext.Inventories.AsNoTracking()
                    .Where(x => x.Id == inventoryId.Value)
                    .Select(x => x.PositionId)
                    .FirstOrDefaultAsync()
                : supplierOrder?.InventoryId.HasValue == true
                    ? await _dbContext.Inventories.AsNoTracking()
                        .Where(x => x.Id == supplierOrder.InventoryId.Value)
                        .Select(x => x.PositionId)
                        .FirstOrDefaultAsync()
                    : null;
            var customerDeliveryAddressId = customerOrder.CustomerDeliveryAddressId
                ?? supplierOrder?.CustomerDeliveryAddressId;
            var customerPositionId = customerDeliveryAddressId.HasValue
                ? await _dbContext.CustomerDeliveryAddresses.AsNoTracking()
                    .Where(x => x.Id == customerDeliveryAddressId.Value)
                    .Select(x => x.PositionId)
                    .FirstOrDefaultAsync()
                : null;

            var (fromPositionId, toPositionId) = deliveryType switch
            {
                1 => (supplierFactoryPositionId, inventoryPositionId),
                2 => (inventoryPositionId, customerPositionId),
                _ => (supplierFactoryPositionId, customerPositionId),
            };

            if (!fromPositionId.HasValue || !toPositionId.HasValue)
            {
                return Ok(new List<CreateNewDeliveryLegRequest>());
            }

            var positions = await _dbContext.Positions.AsNoTracking()
                .Where(x => x.Id == fromPositionId.Value || x.Id == toPositionId.Value)
                .ToDictionaryAsync(x => x.Id);
            if (!positions.TryGetValue(fromPositionId.Value, out var fromPosition)
                || !positions.TryGetValue(toPositionId.Value, out var toPosition))
            {
                return Ok(new List<CreateNewDeliveryLegRequest>());
            }

            return Ok(new List<CreateNewDeliveryLegRequest>
            {
                new()
                {
                    FromPositionId = fromPosition.Id,
                    ToPositionId = toPosition.Id,
                    LegFromPositionId = fromPosition.Id,
                    LegToPositionId = toPosition.Id,
                    TypeOfTransport = "Väg",
                    SortOrder = 1,
                    LatitudeStart = fromPosition.Latitude,
                    LongitudeStart = fromPosition.Longitude,
                    LatitudeEnd = toPosition.Latitude,
                    LongitudeEnd = toPosition.Longitude,
                    FromPositionName = fromPosition.Name ?? string.Empty,
                    FromPositionPostalAddress = fromPosition.PostalAddress ?? string.Empty,
                    ToPositionName = toPosition.Name ?? string.Empty,
                    ToPositionPostalAddress = toPosition.PostalAddress ?? string.Empty,
                },
            });
        }

        [HttpGet("positions")]
        public async Task<ActionResult<List<DeliveryPositionDto>>> SearchDeliveryPositions([FromQuery] string? search)
        {
            var term = search?.Trim();
            if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
            {
                return Ok(new List<DeliveryPositionDto>());
            }

            var positions = await _dbContext.Positions.AsNoTracking()
                .Where(x => (x.Name != null && x.Name.Contains(term))
                    || (x.PostalAddress != null && x.PostalAddress.Contains(term))
                    || (x.PostalNr != null && x.PostalNr.Contains(term)))
                .OrderBy(x => x.Name)
                .Take(25)
                .Select(x => new DeliveryPositionDto
                {
                    Id = x.Id,
                    Name = x.Name ?? string.Empty,
                    PostalAddress = x.PostalAddress ?? string.Empty,
                    Latitude = x.Latitude,
                    Longitude = x.Longitude,
                })
                .ToListAsync();

            return Ok(positions);
        }

        [HttpPost("{type}/{id:int}/delivery-note")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<DocumentFileDto>> UploadDeliveryNote(
            string type,
            int id,
            [FromForm] int supplierOrderId,
            [FromForm] IFormFile? file)
        {
            if (file is null || file.Length == 0)
            {
                return BadRequest("Välj en fil.");
            }

            if (file.Length > 20 * 1024 * 1024)
            {
                return BadRequest("Filen får vara högst 20 MB.");
            }

            var normalizedType = NormalizeType(type);
            var actualSupplierOrderId = normalizedType switch
            {
                "DeliveryToStock" => await _dbContext.DeliveryToStocks.AsNoTracking()
                    .Where(x => x.Id == id)
                    .Select(x => x.SupplierOrderId)
                    .FirstOrDefaultAsync(),
                "DeliveryToCustomer" => await _dbContext.DeliveryToCustomers.AsNoTracking()
                    .Where(x => x.Id == id)
                    .Select(x => x.SupplierOrderId)
                    .FirstOrDefaultAsync(),
                "DeliveryFromStock" => await _dbContext.DeliveryFromStocks.AsNoTracking()
                    .Where(x => x.Id == id)
                    .Select(x => x.CustomerOrder != null && x.CustomerOrder.SupplierOrder != null
                        ? (int?)x.CustomerOrder.SupplierOrder.Id
                        : null)
                    .FirstOrDefaultAsync(),
                _ => null,
            };

            if (!actualSupplierOrderId.HasValue || actualSupplierOrderId.Value != supplierOrderId)
            {
                return BadRequest("Leveransen är inte kopplad till vald leverantörsorder.");
            }

            if (!await _dbContext.DocumentTypes.AnyAsync(x => x.Id == 3))
            {
                return BadRequest("Dokumenttypen för följesedel kunde inte hittas.");
            }

            var originalName = Path.GetFileName(file.FileName.Replace('\\', '/'));
            var extension = Path.GetExtension(originalName);
            var storedName = $"{Guid.NewGuid():N}{extension}";
            var attachmentDirectory = Path.Combine(_webHostEnvironment.ContentRootPath, "Attatchments");
            Directory.CreateDirectory(attachmentDirectory);
            var storedPath = Path.Combine(attachmentDirectory, storedName);

            try
            {
                await using (var stream = System.IO.File.Create(storedPath))
                {
                    await file.CopyToAsync(stream);
                }

                var documentFile = new DocumentFile
                {
                    FileNamePath = Path.Combine("Attatchments", storedName),
                    FileType = file.ContentType,
                    DocumentTypeId = 3,
                    SupplierOrderId = supplierOrderId,
                    Name = originalName,
                    Description = "Följesedel",
                    CreatedDateTime = SwedishTime.Now,
                };

                _dbContext.DocumentFiles.Add(documentFile);
                await _dbContext.SaveChangesAsync();

                return Ok(new DocumentFileDto
                {
                    Id = documentFile.Id,
                    FileNamePath = documentFile.FileNamePath,
                    FileType = documentFile.FileType,
                    DocumentTypeId = documentFile.DocumentTypeId,
                    SupplierOrderId = documentFile.SupplierOrderId,
                    Name = documentFile.Name,
                    Description = documentFile.Description,
                    CreatedDateTime = documentFile.CreatedDateTime,
                });
            }
            catch
            {
                if (System.IO.File.Exists(storedPath))
                {
                    System.IO.File.Delete(storedPath);
                }

                throw;
            }
        }

        [HttpGet("delivery-notes/attachments/{id:int}")]
        public async Task<IActionResult> GetDeliveryNote(int id)
        {
            var documentFile = await _dbContext.DocumentFiles.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.DocumentTypeId == 3);
            if (documentFile?.FileNamePath is null)
            {
                return NotFound();
            }

            var attachmentRoot = Path.GetFullPath(Path.Combine(_webHostEnvironment.ContentRootPath, "Attatchments"));
            var filePath = Path.GetFullPath(Path.Combine(_webHostEnvironment.ContentRootPath, documentFile.FileNamePath));
            if (!filePath.StartsWith(attachmentRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || !System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            return PhysicalFile(filePath, documentFile.FileType ?? "application/octet-stream", documentFile.Name ?? Path.GetFileName(filePath));
        }

        [HttpPost("new")]
        public async Task<ActionResult<CreateNewDeliveryResponse>> CreateNewDelivery([FromBody] CreateNewDeliveryRequest request)
        {
            if (request is null)
            {
                return BadRequest("Ingen data att spara.");
            }

            if (request.DeliveryType is < 1 or > 3)
            {
                return BadRequest("Leveranstypen är ogiltig.");
            }

            if (request.CustomerOrderId <= 0)
            {
                return BadRequest("Order måste väljas.");
            }

            if (request.DeliveryDate is null)
            {
                return BadRequest("Måste ange datum.");
            }

            if (request.ProducedEdition < 0)
            {
                return BadRequest("Producerad upplaga får inte vara negativ.");
            }

            if (request.NrOfItems < 0 || request.NrOfPallets < 0
                || request.SlattNrOfItems < 0 || request.SlattNrOfPallets < 0)
            {
                return BadRequest("Antal på leveransen eller slatten får inte vara negativt.");
            }

            if (request.CallOff?.Length > 50)
            {
                return BadRequest("Avropsreferensen får vara högst 50 tecken.");
            }

            var customerOrder = await _dbContext.CustomerOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.CustomerOrderId);

            if (customerOrder is null)
            {
                return BadRequest("Ordern kunde inte hittas.");
            }

            if (!customerOrder.SupplierOrderId.HasValue
                || !await _dbContext.SupplierOrders.AnyAsync(x => x.Id == customerOrder.SupplierOrderId.Value))
            {
                return BadRequest("Leverantörsorder saknas eller kunde inte hittas.");
            }

            if (request.InventoryId.HasValue
                && !await _dbContext.Inventories.AnyAsync(x => x.Id == request.InventoryId.Value))
            {
                return BadRequest("Valt lager kunde inte hittas.");
            }

            if (request.PalletFormatId.HasValue
                && !await _dbContext.PalletFormats.AnyAsync(x => x.Id == request.PalletFormatId.Value))
            {
                return BadRequest("Valt pallformat kunde inte hittas.");
            }

            var slattWasProvided = request.SlattNrOfItems.HasValue
                || request.SlattNrOfPallets.HasValue
                || request.SlattEditionPerPallet.HasValue;
            if (slattWasProvided && (!request.SlattNrOfItems.HasValue || !request.SlattNrOfPallets.HasValue))
            {
                return BadRequest("Slattens antal och pallantal måste anges tillsammans.");
            }

            var slattDeliveries = request.SlattDeliveries?.ToList()
                ?? new List<CreateNewDeliverySlattRequest>();
            if (slattDeliveries.Count == 0 && slattWasProvided)
            {
                slattDeliveries.Add(new CreateNewDeliverySlattRequest
                {
                    NrOfItems = request.SlattNrOfItems!.Value,
                    NrOfPallets = request.SlattNrOfPallets!.Value,
                    EditionPerPallet = request.SlattEditionPerPallet,
                });
            }

            if (slattDeliveries.Any(x => x.NrOfItems <= 0 || x.NrOfPallets <= 0))
            {
                return BadRequest("Varje slattleverans måste ha ett positivt antal och minst en pall.");
            }

            var positionIds = request.DeliveryLegs
                .SelectMany(x => new[] { x.FromPositionId, x.ToPositionId, x.LegFromPositionId, x.LegToPositionId })
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();
            if (positionIds.Count > 0
                && await _dbContext.Positions.CountAsync(x => positionIds.Contains(x.Id)) != positionIds.Count)
            {
                return BadRequest("En eller flera transportpositioner kunde inte hittas.");
            }

            var deliveryType = request.DeliveryType switch
            {
                1 => "DeliveryToStock",
                2 => "DeliveryFromStock",
                _ => "DeliveryToCustomer",
            };
            var deliveryStatus = request.IsDelivered ? 2 : 1;
            int deliveryId;
            var slattIds = new List<int>();

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            var supplierOrder = await _dbContext.SupplierOrders
                .FirstAsync(x => x.Id == customerOrder.SupplierOrderId!.Value);
            if (supplierOrder.ProducedEdition != request.ProducedEdition)
            {
                supplierOrder.ProducedEdition = request.ProducedEdition;
                supplierOrder.Edited = SwedishTime.Now;
            }

            if (request.DeliveryType == 1)
            {
                var delivery = new DeliveryToStock
                {
                    SupplierOrderId = customerOrder.SupplierOrderId,
                    DeliveryDate = request.DeliveryDate,
                    NrOfItems = request.NrOfItems,
                    NrOfPallets = request.NrOfPallets,
                    DeliveryStatus = deliveryStatus,
                    InventoryId = request.InventoryId,
                    PalletFormatId = request.PalletFormatId,
                    PalletIsStackable = request.PalletIsStackable,
                    PalletLength = request.PalletLength,
                    PalletWidth = request.PalletWidth,
                    PalletHeight = request.PalletHeight,
                    EditionPerPallet = request.EditionPerPallet,
                    PalletCalcFactor = request.PalletCalcFactor,
                    IsSlattPallet = false,
                };

                _dbContext.DeliveryToStocks.Add(delivery);
                await _dbContext.SaveChangesAsync();
                deliveryId = delivery.Id;

                foreach (var slattRequest in slattDeliveries)
                {
                    var slatt = new DeliveryToStock
                    {
                        SupplierOrderId = customerOrder.SupplierOrderId,
                        DeliveryDate = request.DeliveryDate,
                        NrOfItems = slattRequest.NrOfItems,
                        NrOfPallets = slattRequest.NrOfPallets,
                        DeliveryStatus = deliveryStatus,
                        InventoryId = request.InventoryId,
                        PalletFormatId = request.PalletFormatId,
                        PalletIsStackable = request.PalletIsStackable,
                        PalletLength = request.PalletLength,
                        PalletWidth = request.PalletWidth,
                        PalletHeight = request.PalletHeight,
                        PalletCalcFactor = request.PalletCalcFactor,
                        EditionPerPallet = slattRequest.EditionPerPallet,
                        IsSlattPallet = true,
                        ParentDeliveryId = deliveryId,
                    };
                    _dbContext.DeliveryToStocks.Add(slatt);
                    await _dbContext.SaveChangesAsync();
                    slattIds.Add(slatt.Id);
                }
            }
            else if (request.DeliveryType == 2)
            {
                var delivery = new DeliveryFromStock
                {
                    CustomerOrderId = customerOrder.Id,
                    DeliveryDate = request.DeliveryDate,
                    NrOfItems = request.NrOfItems,
                    NrOfPallets = request.NrOfPallets,
                    DeliveryStatus = deliveryStatus,
                    CallOff = request.CallOff,
                    InventoryId = request.InventoryId,
                    PalletFormatId = request.PalletFormatId,
                    PalletIsStackable = request.PalletIsStackable,
                    PalletLength = request.PalletLength,
                    PalletWidth = request.PalletWidth,
                    PalletHeight = request.PalletHeight,
                    EditionPerPallet = request.EditionPerPallet,
                    PalletCalcFactor = request.PalletCalcFactor,
                    IsSlattPallet = false,
                };

                _dbContext.DeliveryFromStocks.Add(delivery);
                await _dbContext.SaveChangesAsync();
                deliveryId = delivery.Id;

                foreach (var slattRequest in slattDeliveries)
                {
                    var slatt = new DeliveryFromStock
                    {
                        CustomerOrderId = customerOrder.Id,
                        DeliveryDate = request.DeliveryDate,
                        NrOfItems = slattRequest.NrOfItems,
                        NrOfPallets = slattRequest.NrOfPallets,
                        DeliveryStatus = deliveryStatus,
                        CallOff = request.CallOff,
                        InventoryId = request.InventoryId,
                        PalletFormatId = request.PalletFormatId,
                        PalletIsStackable = request.PalletIsStackable,
                        PalletLength = request.PalletLength,
                        PalletWidth = request.PalletWidth,
                        PalletHeight = request.PalletHeight,
                        PalletCalcFactor = request.PalletCalcFactor,
                        EditionPerPallet = slattRequest.EditionPerPallet,
                        IsSlattPallet = true,
                        ParentDeliveryId = deliveryId,
                    };
                    _dbContext.DeliveryFromStocks.Add(slatt);
                    await _dbContext.SaveChangesAsync();
                    slattIds.Add(slatt.Id);
                }
            }
            else
            {
                var delivery = new DeliveryToCustomer
                {
                    SupplierOrderId = customerOrder.SupplierOrderId,
                    CustomerOrderId = customerOrder.Id,
                    DeliveryDate = request.DeliveryDate,
                    NrOfItems = request.NrOfItems,
                    NrOfPallets = request.NrOfPallets,
                    DeliveryStatus = deliveryStatus,
                    CallOff = request.CallOff,
                    PalletFormatId = request.PalletFormatId,
                    PalletIsStackable = request.PalletIsStackable,
                    PalletLength = request.PalletLength,
                    PalletWidth = request.PalletWidth,
                    PalletHeight = request.PalletHeight,
                    EditionPerPallet = request.EditionPerPallet,
                    PalletCalcFactor = request.PalletCalcFactor,
                    IsSlattPallet = false,
                };

                _dbContext.DeliveryToCustomers.Add(delivery);
                await _dbContext.SaveChangesAsync();
                deliveryId = delivery.Id;

                foreach (var slattRequest in slattDeliveries)
                {
                    var slatt = new DeliveryToCustomer
                    {
                        SupplierOrderId = customerOrder.SupplierOrderId,
                        CustomerOrderId = customerOrder.Id,
                        DeliveryDate = request.DeliveryDate,
                        NrOfItems = slattRequest.NrOfItems,
                        NrOfPallets = slattRequest.NrOfPallets,
                        DeliveryStatus = deliveryStatus,
                        CallOff = request.CallOff,
                        PalletFormatId = request.PalletFormatId,
                        PalletIsStackable = request.PalletIsStackable,
                        PalletLength = request.PalletLength,
                        PalletWidth = request.PalletWidth,
                        PalletHeight = request.PalletHeight,
                        PalletCalcFactor = request.PalletCalcFactor,
                        EditionPerPallet = slattRequest.EditionPerPallet,
                        IsSlattPallet = true,
                        ParentDeliveryId = deliveryId,
                    };
                    _dbContext.DeliveryToCustomers.Add(slatt);
                    await _dbContext.SaveChangesAsync();
                    slattIds.Add(slatt.Id);
                }
            }

            foreach (var legRequest in request.DeliveryLegs.OrderBy(x => x.SortOrder))
            {
                _dbContext.DeliveryLegs.Add(new DeliveryLeg
                {
                    DeliveryToStockId = request.DeliveryType == 1 ? deliveryId : null,
                    DeliveryFromStockId = request.DeliveryType == 2 ? deliveryId : null,
                    DeliveryToCustomerId = request.DeliveryType == 3 ? deliveryId : null,
                    FromPositionId = legRequest.FromPositionId,
                    ToPositionId = legRequest.ToPositionId,
                    FromPositionIdLeg = legRequest.LegFromPositionId,
                    ToPositionIdLeg = legRequest.LegToPositionId,
                    PositionDistanceId = legRequest.PositionDistanceId,
                    DistanceKm = legRequest.DistanceKm,
                    TypeOfTransport = legRequest.TypeOfTransport,
                    SortOrder = legRequest.SortOrder,
                    LatitudeStart = legRequest.LatitudeStart,
                    LongitudeStart = legRequest.LongitudeStart,
                    LatitudeEnd = legRequest.LatitudeEnd,
                    LongitudeEnd = legRequest.LongitudeEnd,
                });
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            var response = new CreateNewDeliveryResponse
            {
                Type = deliveryType,
                Id = deliveryId,
                SlattId = slattIds.Count > 0 ? slattIds[0] : (int?)null,
                SlattIds = slattIds,
            };

            return CreatedAtAction(nameof(GetByTypeAndId), new { type = deliveryType, id = deliveryId }, response);
        }

        [HttpGet("{type}/{id:int}")]
        public async Task<ActionResult<DeliveryDto>> GetByTypeAndId(string type, int id)
        {
            var normalizedType = NormalizeType(type);

            var result = normalizedType switch
            {
                "DeliveryToCustomer" => await QueryToCustomer()
                    .FirstOrDefaultAsync(x => x.Id == id),
                "DeliveryToStock" => await QueryToStock()
                    .FirstOrDefaultAsync(x => x.Id == id),
                "DeliveryFromStock" => await QueryFromStock()
                    .FirstOrDefaultAsync(x => x.Id == id),
                _ => null
            };

            return result is null ? NotFound() : Ok(result);
        }

        [HttpPost("search")]
        public async Task<ActionResult<PagedResultDto<DeliveryDto>>> Search([FromBody] SearchDeliveriesRequestDto? request)
        {
            var pagination = request?.Pagination ?? new PaginationRequest();

            IQueryable<DeliveryDto> query = QueryToCustomer()
                .Concat(QueryToStock())
                .Concat(QueryFromStock());

            if (request?.Filter?.Conditions?.Count > 0)
            {
                foreach (var condition in request.Filter.Conditions)
                {
                    try
                    {
                        query = ApplyCondition(query, condition);
                    }
                    catch (ArgumentException ex)
                    {
                        _logger.LogWarning(ex, "Invalid delivery search condition for field {Field}", condition.Field);
                        return BadRequest(new { message = ex.Message, field = condition.Field });
                    }
                }
            }

            IOrderedQueryable<DeliveryDto> ordered;
            try
            {
                ordered = ApplyOrdering(query, request?.OrderBy);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid delivery order by request");
                return BadRequest(new { message = ex.Message });
            }

            var totalCount = await query.CountAsync();
            var totalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pagination.PageSize);

            var items = await ordered
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync();

            return Ok(new PagedResultDto<DeliveryDto>
            {
                Items = items,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            });
        }

        private IQueryable<DeliveryDto> QueryToCustomer()
        {
            return _dbContext.DeliveryToCustomers
                .AsNoTracking()
                .Select(x => new DeliveryDto
                {
                    Id = x.Id,
                    Type = "DeliveryToCustomer",
                    SupplierOrderId = x.SupplierOrderId,
                    CustomerOrderId = x.CustomerOrderId,
                    DeliveryDate = x.DeliveryDate,
                    NrOfItems = x.NrOfItems,
                    ToInvoice = x.ToInvoice,
                    NrOfPallets = x.NrOfPallets,
                    CallOff = x.CallOff,
                    OldDbId = x.OldDbId,
                    SpcsRefNr = x.SpcsRefNr,
                    SupplierInvoiceNr = x.SupplierInvoiceNr,
                    SupplierInvoiceCost = x.SupplierInvoiceCost,
                    DeliveryStatus = x.DeliveryStatus,
                    PalletFormatId = x.PalletFormatId,
                    PalletIsStackable = x.PalletIsStackable,
                    PalletWidth = x.PalletWidth,
                    PalletHeight = x.PalletHeight,
                    PalletLength = x.PalletLength,
                    EditionPerPallet = x.EditionPerPallet,
                    PalletCalcFactor = x.PalletCalcFactor,
                    IsSlattPallet = x.IsSlattPallet,
                    ParentDeliveryId = x.ParentDeliveryId,
                    DoNotInvoice = x.DoNotInvoice,
                    InfoOk = x.InfoOk,
                    WeightMissingEmailSentDateTime = x.WeightMissingEmailSentDateTime,
                    CostMissingEmailSentDateTime = x.CostMissingEmailSentDateTime,
                    IsPalletInvoicedSeparately = x.IsPalletInvoicedSeparately,
                    CustomerId = x.CustomerOrder != null ? x.CustomerOrder.CustomerId : null,
                    ProductName = x.CustomerOrder != null ? x.CustomerOrder.Product : null,
                    CustomerName = x.CustomerOrder != null ? x.CustomerOrder.CustomerName : null,
                    SupplierOrderNr = x.CustomerOrder != null && x.CustomerOrder.SupplierOrder != null
                        ? x.CustomerOrder.SupplierOrder.SupplierOrderNr
                        : null,
                    CustomersOrderNr = x.CustomerOrder != null && x.CustomerOrder.SupplierOrder != null
                        ? x.CustomerOrder.SupplierOrder.CustomerOrderNr
                        : null,
                    InventoryId = x.CustomerOrder != null && x.CustomerOrder.SupplierOrder != null
                        ? x.CustomerOrder.SupplierOrder.InventoryId
                        : null,
                    InventoryName = x.CustomerOrder != null && x.CustomerOrder.SupplierOrder != null && x.CustomerOrder.SupplierOrder.Inventory != null
                        ? x.CustomerOrder.SupplierOrder.Inventory.Name
                        : null,
                    TransportOrderId = _dbContext.TransportOrderDeliveries
                        .Where(t => t.DeliveryToCustomerId == x.Id)
                        .Select(t => t.TransportOrderId)
                        .FirstOrDefault(),
                    TransportOrderNr = _dbContext.TransportOrderDeliveries
                        .Where(t => t.DeliveryToCustomerId == x.Id)
                        .Select(t => t.TransportOrder != null ? (int?)t.TransportOrder.TransportOrderNr : null)
                        .FirstOrDefault(),

                    // Assigned to keep the Concat/union branches identical
                    DeliveryNr = null,
                    IsAdjustment = null,
                    AdjustedFromDeliveryId = null,
                    DeliveryToStockId = null,
                    NrOfBunt = null,
                    NrOfYtterforpackning = null,
                    CallOffNr = null
                });
        }

        private IQueryable<DeliveryDto> QueryToStock()
        {
            return _dbContext.DeliveryToStocks
                .AsNoTracking()
                .Select(x => new DeliveryDto
                {
                    Id = x.Id,
                    Type = "DeliveryToStock",
                    SupplierOrderId = x.SupplierOrderId,
                    DeliveryDate = x.DeliveryDate,
                    NrOfItems = x.NrOfItems,
                    NrOfPallets = x.NrOfPallets,
                    DeliveryNr = x.DeliveryNr,
                    InventoryId = x.InventoryId,
                    OldDbId = x.OldDbId,
                    SpcsRefNr = x.SpcsRefNr,
                    SupplierInvoiceNr = x.SupplierInvoiceNr,
                    SupplierInvoiceCost = x.SupplierInvoiceCost,
                    DeliveryStatus = x.DeliveryStatus,
                    PalletFormatId = x.PalletFormatId,
                    PalletIsStackable = x.PalletIsStackable,
                    PalletWidth = x.PalletWidth,
                    PalletHeight = x.PalletHeight,
                    PalletLength = x.PalletLength,
                    EditionPerPallet = x.EditionPerPallet,
                    PalletCalcFactor = x.PalletCalcFactor,
                    IsSlattPallet = x.IsSlattPallet,
                    ParentDeliveryId = x.ParentDeliveryId,
                    InfoOk = x.InfoOk,
                    IsAdjustment = x.IsAdjustment,
                    AdjustedFromDeliveryId = x.AdjustedFromDeliveryId,
                    WeightMissingEmailSentDateTime = x.WeightMissingEmailSentDateTime,
                    CostMissingEmailSentDateTime = x.CostMissingEmailSentDateTime,
                    ProductName = x.SupplierOrder != null ? x.SupplierOrder.Product : null,
                    CustomerName = x.SupplierOrder != null ? x.SupplierOrder.Customer2 : null,
                    SupplierOrderNr = x.SupplierOrder != null ? x.SupplierOrder.SupplierOrderNr : null,
                    CustomersOrderNr = x.SupplierOrder != null ? x.SupplierOrder.CustomerOrderNr : null,
                    InventoryName = x.Inventory != null ? x.Inventory.Name : null,
                    TransportOrderId = _dbContext.TransportOrderDeliveries
                        .Where(t => t.DeliveryToStockId == x.Id)
                        .Select(t => t.TransportOrderId)
                        .FirstOrDefault(),
                    TransportOrderNr = _dbContext.TransportOrderDeliveries
                        .Where(t => t.DeliveryToStockId == x.Id)
                        .Select(t => t.TransportOrder != null ? (int?)t.TransportOrder.TransportOrderNr : null)
                        .FirstOrDefault(),

                    // Assigned to keep the Concat/union branches identical
                    CustomerOrderId = null,
                    ToInvoice = null,
                    CallOff = null,
                    DoNotInvoice = null,
                    IsPalletInvoicedSeparately = null,
                    DeliveryToStockId = null,
                    NrOfBunt = null,
                    NrOfYtterforpackning = null,
                    CustomerId = null,
                    CallOffNr = null
                });
        }

        private IQueryable<DeliveryDto> QueryFromStock()
        {
            return _dbContext.DeliveryFromStocks
                .AsNoTracking()
                .Select(x => new DeliveryDto
                {
                    Id = x.Id,
                    Type = "DeliveryFromStock",
                    CustomerOrderId = x.CustomerOrderId,
                    DeliveryDate = x.DeliveryDate,
                    NrOfItems = x.NrOfItems,
                    ToInvoice = x.ToInvoice,
                    NrOfPallets = x.NrOfPallets,
                    CallOff = x.CallOff,
                    InventoryId = x.InventoryId,
                    OldDbId = x.OldDbId,
                    DeliveryStatus = x.DeliveryStatus,
                    PalletFormatId = x.PalletFormatId,
                    PalletIsStackable = x.PalletIsStackable,
                    PalletWidth = x.PalletWidth,
                    PalletHeight = x.PalletHeight,
                    PalletLength = x.PalletLength,
                    EditionPerPallet = x.EditionPerPallet,
                    PalletCalcFactor = x.PalletCalcFactor,
                    IsSlattPallet = x.IsSlattPallet,
                    ParentDeliveryId = x.ParentDeliveryId,
                    DeliveryToStockId = x.DeliveryToStockId,
                    DoNotInvoice = x.DoNotInvoice,
                    InfoOk = x.InfoOk,
                    WeightMissingEmailSentDateTime = x.WeightMissingEmailSentDateTime,
                    CostMissingEmailSentDateTime = x.CostMissingEmailSentDateTime,
                    NrOfBunt = x.NrOfBunt,
                    NrOfYtterforpackning = x.NrOfYtterforpackning,
                    IsPalletInvoicedSeparately = x.IsPalletInvoicedSeparately,
                    CustomerId = x.CustomerOrder != null ? x.CustomerOrder.CustomerId : null,
                    ProductName = x.CustomerOrder != null ? x.CustomerOrder.Product : null,
                    CustomerName = x.CustomerOrder != null ? x.CustomerOrder.CustomerName : null,
                    SupplierOrderNr = x.CustomerOrder != null && x.CustomerOrder.SupplierOrder != null
                        ? x.CustomerOrder.SupplierOrder.SupplierOrderNr
                        : null,
                    CustomersOrderNr = x.CustomerOrder != null && x.CustomerOrder.SupplierOrder != null
                        ? x.CustomerOrder.SupplierOrder.CustomerOrderNr
                        : null,
                    InventoryName = x.Inventory != null ? x.Inventory.Name : null,
                    TransportOrderId = _dbContext.TransportOrderDeliveries
                        .Where(t => t.DeliveryFromStockId == x.Id)
                        .Select(t => t.TransportOrderId)
                        .FirstOrDefault(),
                    TransportOrderNr = _dbContext.TransportOrderDeliveries
                        .Where(t => t.DeliveryFromStockId == x.Id)
                        .Select(t => t.TransportOrder != null ? (int?)t.TransportOrder.TransportOrderNr : null)
                        .FirstOrDefault(),
                    CallOffNr = _dbContext.CallOffDeliveries
                        .Where(c => c.DeliveryFromStockId == x.Id)
                        .Select(c => c.CallOffId)
                        .FirstOrDefault(),

                    // Assigned to keep the Concat/union branches identical
                    SupplierOrderId = x.CustomerOrder != null && x.CustomerOrder.SupplierOrder != null
                        ? x.CustomerOrder.SupplierOrder.Id
                        : null,
                    SpcsRefNr = null,
                    SupplierInvoiceNr = null,
                    SupplierInvoiceCost = null,
                    DeliveryNr = null,
                    IsAdjustment = null,
                    AdjustedFromDeliveryId = null
                });
        }

        private static Dictionary<string, PropertyInfo> BuildFieldMap()
        {
            var map = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in typeof(DeliveryDto).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                map[property.Name.ToLowerInvariant()] = property;
            }

            map["lngdeliverytocustomer_id"] = map["id"];
            map["lngdeliverytostock_id"] = map["id"];
            map["lngdeliveryfromstock_id"] = map["id"];
            map["dtedeliverydate"] = map["deliverydate"];
            map["lngsupplierorder_id"] = map["supplierorderid"];
            map["lngcustomerorder_id"] = map["customerorderid"];
            map["lngnrofitems"] = map["nrofitems"];
            map["lngnrofpallets"] = map["nrofpallets"];

            return map;
        }

        private static string NormalizeType(string type)
        {
            var value = type.Trim().ToLowerInvariant();
            return value switch
            {
                "deliverytocustomer" or "tocustomer" => "DeliveryToCustomer",
                "deliverytostock" or "tostock" => "DeliveryToStock",
                "deliveryfromstock" or "fromstock" => "DeliveryFromStock",
                _ => string.Empty
            };
        }

        private static PropertyInfo ResolveProperty(string field)
        {
            var key = field.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Filter field is required.");
            }

            if (!FieldMap.TryGetValue(key, out var property))
            {
                throw new ArgumentException($"Unsupported field '{field}'.");
            }

            return property;
        }

        private static IOrderedQueryable<DeliveryDto> ApplyOrdering(
            IQueryable<DeliveryDto> query,
            List<SortRequest>? orderBy)
        {
            if (orderBy is null || orderBy.Count == 0)
            {
                return query.OrderBy(x => x.Type).ThenBy(x => x.Id);
            }

            IOrderedQueryable<DeliveryDto>? ordered = null;

            foreach (var sort in orderBy)
            {
                var property = ResolveProperty(sort.Field);
                var isDescending = string.Equals(sort.Direction, "desc", StringComparison.OrdinalIgnoreCase);
                ordered = ApplySort(ordered, query, property, isDescending);
            }

            return ordered ?? query.OrderBy(x => x.Type).ThenBy(x => x.Id);
        }

        private static IOrderedQueryable<DeliveryDto> ApplySort(
            IOrderedQueryable<DeliveryDto>? ordered,
            IQueryable<DeliveryDto> source,
            PropertyInfo property,
            bool isDescending)
        {
            var parameter = Expression.Parameter(typeof(DeliveryDto), "x");
            var propertyExpression = Expression.Property(parameter, property);
            var lambda = Expression.Lambda(propertyExpression, parameter);

            var methodName = ordered is null
                ? (isDescending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy))
                : (isDescending ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy));

            var method = typeof(Queryable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(DeliveryDto), property.PropertyType);

            var result = method.Invoke(null, new object[] { ordered ?? source, lambda });
            return (IOrderedQueryable<DeliveryDto>)result!;
        }

        private static IQueryable<DeliveryDto> ApplyCondition(IQueryable<DeliveryDto> query, FilterConditionDto condition)
        {
            var property = ResolveProperty(condition.Field);
            var op = condition.Operator.Trim().ToLowerInvariant();

            var parameter = Expression.Parameter(typeof(DeliveryDto), "x");
            var member = Expression.Property(parameter, property);
            var body = BuildConditionBody(member, property.PropertyType, condition, op);

            var lambda = Expression.Lambda<Func<DeliveryDto, bool>>(body, parameter);
            return query.Where(lambda);
        }

        private static Expression BuildConditionBody(
            MemberExpression member,
            Type memberType,
            FilterConditionDto condition,
            string op)
        {
            var underlyingType = Nullable.GetUnderlyingType(memberType) ?? memberType;
            var supportsNull = !memberType.IsValueType || Nullable.GetUnderlyingType(memberType) is not null;

            if (op == "isnull")
            {
                if (!supportsNull)
                {
                    throw new ArgumentException($"Operator '{condition.Operator}' is not valid for non-nullable field '{condition.Field}'.");
                }

                return Expression.Equal(member, Expression.Constant(null, memberType));
            }

            if (op == "isnotnull")
            {
                if (!supportsNull)
                {
                    throw new ArgumentException($"Operator '{condition.Operator}' is not valid for non-nullable field '{condition.Field}'.");
                }

                return Expression.NotEqual(member, Expression.Constant(null, memberType));
            }

            if (underlyingType == typeof(string))
            {
                return BuildStringCondition(member, condition, op);
            }

            if (underlyingType == typeof(bool))
            {
                var value = GetRequiredSingleValue(condition, underlyingType);
                return BuildComparableCondition(member, memberType, value, condition, op, allowRange: false);
            }

            if (IsSupportedComparableType(underlyingType))
            {
                if (op == "in")
                {
                    return BuildInCondition(member, memberType, underlyingType, condition);
                }

                var value = GetRequiredSingleValue(condition, underlyingType);
                return BuildComparableCondition(member, memberType, value, condition, op, allowRange: true);
            }

            throw new ArgumentException($"Unsupported field type for '{condition.Field}'.");
        }

        private static Expression BuildStringCondition(MemberExpression member, FilterConditionDto condition, string op)
        {
            if (string.Equals(condition.Field, nameof(DeliveryDto.Type), StringComparison.OrdinalIgnoreCase))
            {
                return BuildTypeCondition(member, condition, op);
            }

            var value = GetSingleStringValue(condition);
            var nullConstant = Expression.Constant(null, typeof(string));

            return op switch
            {
                "eq" => Expression.Equal(member, CreateValueExpression(value, typeof(string))),
                "neq" => Expression.NotEqual(member, CreateValueExpression(value, typeof(string))),
                "contains" when value is not null => BuildStringMethodCall(member, value, nameof(string.Contains)),
                "startswith" when value is not null => BuildStringMethodCall(member, value, nameof(string.StartsWith)),
                "endswith" when value is not null => BuildStringMethodCall(member, value, nameof(string.EndsWith)),
                "in" => BuildStringInCondition(member, GetStringValues(condition)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for string field '{condition.Field}'.")
            };

            Expression BuildStringMethodCall(Expression left, string right, string methodName)
            {
                var method = typeof(string).GetMethod(methodName, new[] { typeof(string) })!;
                return Expression.AndAlso(
                    Expression.NotEqual(left, nullConstant),
                    Expression.Call(left, method, CreateValueExpression(right, typeof(string))));
            }
        }

        private static Expression BuildTypeCondition(MemberExpression member, FilterConditionDto condition, string op)
        {
            var nullConstant = Expression.Constant(null, typeof(string));

            return op switch
            {
                "eq" => Expression.Equal(member, CreateValueExpression(NormalizeRequiredTypeValue(GetSingleStringValue(condition), condition.Field), typeof(string))),
                "neq" => Expression.NotEqual(member, CreateValueExpression(NormalizeRequiredTypeValue(GetSingleStringValue(condition), condition.Field), typeof(string))),
                "in" => BuildStringInCondition(member, GetNormalizedTypeValues(condition)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for field '{condition.Field}'. Use eq, neq, or in.")
            };

            static List<string> GetNormalizedTypeValues(FilterConditionDto condition)
            {
                var values = GetStringValues(condition)
                    .Select(v => NormalizeRequiredTypeValue(v, condition.Field))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                if (values.Count == 0)
                {
                    throw new ArgumentException($"Filter field '{condition.Field}' requires at least one valid type value.");
                }

                return values;
            }
        }

        private static Expression BuildStringInCondition(MemberExpression member, List<string> values)
        {
            if (values.Count == 0)
            {
                throw new ArgumentException("Operator 'in' requires at least one value.");
            }

            var nullConstant = Expression.Constant(null, typeof(string));
            var containsMethod = typeof(List<string>).GetMethod(nameof(List<string>.Contains), new[] { typeof(string) })!;

            return Expression.AndAlso(
                Expression.NotEqual(member, nullConstant),
                Expression.Call(CreateValueExpression(values, typeof(List<string>)), containsMethod, member));
        }

        private static Expression BuildInCondition(
            MemberExpression member,
            Type memberType,
            Type underlyingType,
            FilterConditionDto condition)
        {
            var values = GetValues(condition)
                .Select(value => ConvertJsonValue(value, underlyingType, condition.Field))
                .ToList();

            if (values.Count == 0)
            {
                throw new ArgumentException($"Operator 'in' for field '{condition.Field}' requires values.");
            }

            var listType = typeof(List<>).MakeGenericType(underlyingType);
            var list = Activator.CreateInstance(listType)!;
            var addMethod = listType.GetMethod("Add")!;

            foreach (var value in values)
            {
                addMethod.Invoke(list, new[] { value });
            }

            var containsMethod = listType.GetMethod("Contains", new[] { underlyingType })!;

            if (Nullable.GetUnderlyingType(memberType) is not null)
            {
                var hasValue = Expression.Property(member, nameof(Nullable<int>.HasValue));
                var memberValue = Expression.Property(member, nameof(Nullable<int>.Value));
                return Expression.AndAlso(
                    hasValue,
                    Expression.Call(CreateValueExpression(list, listType), containsMethod, memberValue));
            }

            return Expression.Call(CreateValueExpression(list, listType), containsMethod, member);
        }

        private static Expression BuildComparableCondition(
            MemberExpression member,
            Type memberType,
            object value,
            FilterConditionDto condition,
            string op,
            bool allowRange)
        {
            var constant = BuildConstant(memberType, value);

            if (Nullable.GetUnderlyingType(memberType) is not null && op is "gt" or "gte" or "lt" or "lte")
            {
                var hasValue = Expression.Property(member, nameof(Nullable<int>.HasValue));
                var memberValue = Expression.Property(member, nameof(Nullable<int>.Value));
                var valueConstant = CreateValueExpression(value, value.GetType());
                var comparison = BuildComparison(memberValue, valueConstant, condition, op, allowRange);
                return Expression.AndAlso(hasValue, comparison);
            }

            return BuildComparison(member, constant, condition, op, allowRange);
        }

        private static Expression BuildComparison(
            Expression left,
            Expression right,
            FilterConditionDto condition,
            string op,
            bool allowRange)
        {
            return op switch
            {
                "eq" => Expression.Equal(left, right),
                "neq" => Expression.NotEqual(left, right),
                "gt" when allowRange => Expression.GreaterThan(left, right),
                "gte" when allowRange => Expression.GreaterThanOrEqual(left, right),
                "lt" when allowRange => Expression.LessThan(left, right),
                "lte" when allowRange => Expression.LessThanOrEqual(left, right),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for field '{condition.Field}'.")
            };
        }

        private static Expression BuildConstant(Type targetType, object value)
        {
            var nonNullableTarget = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (nonNullableTarget == targetType)
            {
                return CreateValueExpression(value, targetType);
            }

            return Expression.Convert(CreateValueExpression(value, nonNullableTarget), targetType);
        }

        // Values must be exposed as a closure field access (the exact expression shape C#
        // captures produce). A plain Expression.Constant gets inlined by EF Core as a SQL
        // string literal (e.g. '2026-01-01T00:00:00.0000000'), which SQL Server cannot
        // convert to the legacy datetime columns ("Conversion failed when converting date
        // and/or time from character string"). Closure values are sent as typed SQL
        // parameters instead.
        private static Expression CreateValueExpression(object? value, Type type)
        {
            var holder = new QueryValueHolder { Value = value };
            return Expression.Convert(
                Expression.Field(Expression.Constant(holder), nameof(QueryValueHolder.Value)),
                type);
        }

        private sealed class QueryValueHolder
        {
            public object? Value;
        }

        private static object GetRequiredSingleValue(FilterConditionDto condition, Type targetType)
        {
            if (!condition.Value.HasValue)
            {
                throw new ArgumentException($"Filter field '{condition.Field}' requires a value.");
            }

            return ConvertJsonValue(condition.Value.Value, targetType, condition.Field)
                ?? throw new ArgumentException($"Filter field '{condition.Field}' requires a non-null value.");
        }

        private static string? GetSingleStringValue(FilterConditionDto condition)
        {
            if (!condition.Value.HasValue)
            {
                return null;
            }

            var value = condition.Value.Value;
            return value.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => bool.TrueString.ToLowerInvariant(),
                JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
                _ => throw new ArgumentException($"Filter field '{condition.Field}' requires a simple value.")
            };
        }

        private static List<string> GetStringValues(FilterConditionDto condition)
        {
            return GetValues(condition)
                .Select(value => value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.Number => value.GetRawText(),
                    JsonValueKind.True => bool.TrueString.ToLowerInvariant(),
                    JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
                    JsonValueKind.Null => null,
                    _ => throw new ArgumentException($"Filter field '{condition.Field}' requires a flat array of values.")
                })
                .Where(value => value is not null)
                .Cast<string>()
                .ToList();
        }

        private static IEnumerable<JsonElement> GetValues(FilterConditionDto condition)
        {
            if (condition.Values is { Count: > 0 })
            {
                return condition.Values;
            }

            if (condition.Value.HasValue && condition.Value.Value.ValueKind == JsonValueKind.Array)
            {
                return condition.Value.Value.EnumerateArray().ToList();
            }

            return Array.Empty<JsonElement>();
        }

        private static object? ConvertJsonValue(JsonElement element, Type targetType, string field)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            try
            {
                if (targetType == typeof(string))
                {
                    return element.ValueKind switch
                    {
                        JsonValueKind.String => element.GetString(),
                        JsonValueKind.Number => element.GetRawText(),
                        JsonValueKind.True => bool.TrueString.ToLowerInvariant(),
                        JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
                        _ => throw new ArgumentException()
                    };
                }

                if (targetType == typeof(int))
                {
                    if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var intNumber))
                    {
                        return intNumber;
                    }

                    if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var intParsed))
                    {
                        return intParsed;
                    }
                }

                if (targetType == typeof(double))
                {
                    if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var doubleNumber))
                    {
                        return doubleNumber;
                    }

                    if (element.ValueKind == JsonValueKind.String && double.TryParse(element.GetString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var doubleParsed))
                    {
                        return doubleParsed;
                    }
                }

                if (targetType == typeof(decimal))
                {
                    if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var decimalNumber))
                    {
                        return decimalNumber;
                    }

                    if (element.ValueKind == JsonValueKind.String && decimal.TryParse(element.GetString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var decimalParsed))
                    {
                        return decimalParsed;
                    }
                }

                if (targetType == typeof(bool))
                {
                    if (element.ValueKind == JsonValueKind.True)
                    {
                        return true;
                    }

                    if (element.ValueKind == JsonValueKind.False)
                    {
                        return false;
                    }

                    if (element.ValueKind == JsonValueKind.String && bool.TryParse(element.GetString(), out var boolParsed))
                    {
                        return boolParsed;
                    }
                }

                if (targetType == typeof(DateTime))
                {
                    if (element.ValueKind == JsonValueKind.String && DateTime.TryParse(element.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTime))
                    {
                        return dateTime;
                    }
                }
            }
            catch (Exception)
            {
                throw new ArgumentException($"Filter field '{field}' has an invalid value.");
            }

            throw new ArgumentException($"Filter field '{field}' has an invalid value.");
        }

        private static bool IsSupportedComparableType(Type type)
        {
            return type == typeof(int)
                || type == typeof(double)
                || type == typeof(decimal)
                || type == typeof(DateTime);
        }

        private static string NormalizeRequiredTypeValue(string? value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"Filter field '{field}' requires a non-empty value.");
            }

            var normalized = NormalizeType(value);
            if (string.IsNullOrEmpty(normalized))
            {
                throw new ArgumentException($"Filter field '{field}' has invalid type '{value}'. Allowed values: {string.Join(", ", AllowedDeliveryTypes)}.");
            }

            return normalized;
        }
    }
}