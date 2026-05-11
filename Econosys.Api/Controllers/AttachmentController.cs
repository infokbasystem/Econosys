
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.StaticFiles;
using System.IO;
using System.Threading.Tasks;

using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;

namespace Netpack.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AttachmentController : ControllerBase
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IContentTypeProvider _contentTypeProvider;
        private readonly ApplicationDbContext _context;

        public AttachmentController(IWebHostEnvironment webHostEnvironment, IContentTypeProvider contentTypeProvider, ApplicationDbContext context)
        {
            _webHostEnvironment = webHostEnvironment;
            _contentTypeProvider = contentTypeProvider;
            _context = context;
        }


        // [HttpGet("{fileName}")]
        // public IActionResult Get(string fileName)
        // {
        //     // Prevent path traversal attacks
        //     if (fileName.Contains(".."))
        //         return BadRequest();

        //     // var filePath = Path.Combine(
        //     //     "C:\\SecureFiles\\attachments",
        //     //     fileName
        //     // );
        //     string contentRootPath = _webHostEnvironment.ContentRootPath;
        //     var filePath = Path.Combine(contentRootPath, "Attachments", fileName);

        //     if (!System.IO.File.Exists(filePath))
        //         return NotFound();

        //     if (!_contentTypeProvider.TryGetContentType(fileName, out var contentType))
        //         contentType = "application/octet-stream";

        //     var fileStream = System.IO.File.OpenRead(filePath);

        //     // Inline display in browser (PDF, images)
        //     return File(fileStream, contentType);
        // }



        // [HttpPost("upload")]
        // [Authorize]
        // [Consumes("multipart/form-data")]
        // public async Task<IActionResult> UploadFile(
        //     IFormFile file,
        //     [FromForm] int? id,
        //     [FromForm] string name,
        //     [FromForm] string? description,
        //     [FromForm] string? folder,
        //     [FromForm] int? productId,
        //     [FromForm] int? calculationId,
        //     [FromForm] int? inquiryId)
        // {
        //     if (file == null || file.Length == 0)
        //         return BadRequest("No file uploaded.");

        //     // string path = "";
        //     string contentRootPath = _webHostEnvironment.ContentRootPath;
        //     var relativePath = Path.Combine("Attachments", file.FileName);
        //     var filePath = Path.Combine(contentRootPath, "Attachments", file.FileName);

        //     // var filePath = Path.Combine("Attachments", file.FileName);

        //     try
        //     {
        //         using (var stream = new FileStream(filePath, FileMode.Create))
        //         {
        //             await file.CopyToAsync(stream);
        //         }

        //         var userIdClaim = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        //                          ?? User?.FindFirst("sub")?.Value
        //                          ?? User?.FindFirst("id")?.Value;
        //         _ = int.TryParse(userIdClaim, out int userId);
        //         var userName = userId > 0
        //             ? await _context.Users
        //                 .Where(u => u.Id == userId)
        //                 .Select(u => u.Name)
        //                 .FirstOrDefaultAsync() ?? string.Empty
        //             : string.Empty;

        //         Attachment? attachmentInDb = null;
        //         if (id == null || id == 0)
        //         {
        //             attachmentInDb = new Attachment();
        //             attachmentInDb.CreatedByUserId = userId;
        //             attachmentInDb.CreatedDate = DateTime.UtcNow;
        //             _context.Attachments.Add(attachmentInDb);
        //         }
        //         else
        //         {
        //             attachmentInDb = await _context.Attachments.FindAsync(id);
        //             if (attachmentInDb == null)
        //                 return NotFound("Attachment not found.");
        //         }
        //         attachmentInDb.CalculationId = calculationId;
        //         attachmentInDb.ContentType = file.ContentType;
        //         attachmentInDb.Description = description ?? string.Empty;
        //         attachmentInDb.EditedByUserId = userId;
        //         attachmentInDb.EditedDate = DateTime.UtcNow;
        //         attachmentInDb.FileName = file.FileName;
        //         attachmentInDb.InquiryId = inquiryId;
        //         attachmentInDb.Name = name;
        //         attachmentInDb.Path = relativePath;
        //         attachmentInDb.ProductId = productId;
        //         attachmentInDb.Size = file.Length;
        //         await _context.SaveChangesAsync();

        //         return Ok(new
        //         {
        //             id = attachmentInDb.Id,
        //             name,
        //             description,
        //             path = filePath,
        //             url = $"api/attachments/{file.FileName}",
        //             lastSavedDateTime = attachmentInDb.EditedDate ?? attachmentInDb.CreatedDate,
        //             lastSavedByUserName = userName
        //         });
        //     }
        //     catch (Exception ex)
        //     {
        //         return BadRequest(ex.Message + ". " + ex.InnerException?.Message + ". " + filePath);
        //     }
        // }

        // [HttpDelete("{id}")]
        // [Authorize]
        // public async Task<IActionResult> DeleteAttachment(int id)
        // {
        //     try
        //     {
        //         var attachmentInDb = await _context.Attachments.FindAsync(id);
        //         if (attachmentInDb == null)
        //             return NotFound("Attachment not found.");

        //         if (System.IO.File.Exists(attachmentInDb.Path))
        //         {
        //             System.IO.File.Delete(attachmentInDb.Path);
        //         }

        //         _context.Attachments.Remove(attachmentInDb);
        //         await _context.SaveChangesAsync();

        //         return NoContent();
        //     }
        //     catch (DbUpdateException e)
        //     {
        //         return BadRequest(e.InnerException?.Message ?? "An error occurred while deleting.");
        //     }
        // }

    }
}
