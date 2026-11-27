using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RentAgency.Web.Features.Accounts;
using RentAgency.Web.Features.Complaints.Application;

namespace RentAgency.Web.Features.Complaints;

[Authorize]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ComplaintEvidenceController(
    IComplaintService complaints,
    UserManager<ApplicationUser> users) : ControllerBase
{
    [HttpGet("/complaints/{complaintId:guid}/evidence/{evidenceId:guid}")]
    public async Task<IActionResult> Download(
        [FromRoute] Guid complaintId,
        [FromRoute] Guid evidenceId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(users.GetUserId(User), out var viewerId))
        {
            return NotFound();
        }

        var details = await complaints.GetAsync(complaintId, viewerId, cancellationToken);
        var metadata = details.Evidence.FirstOrDefault(e => e.Id == evidenceId);

        if (metadata is null)
        {
            return NotFound();
        }

        var stream = await complaints.OpenEvidenceAsync(complaintId, evidenceId, viewerId, cancellationToken);

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(stream, metadata.ContentType, metadata.OriginalFileName);
    }
}
