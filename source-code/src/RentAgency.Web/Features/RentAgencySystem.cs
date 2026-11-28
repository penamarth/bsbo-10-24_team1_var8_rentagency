using RentAgency.Web.Features.Complaints.Application;
using RentAgency.Web.Features.Complaints.Domain;
using RentAgency.Web.Features.Housing.Application;
using RentAgency.Web.Features.Housing.Domain;
using RentAgency.Web.Features.Rentals.Application;
using RentAgency.Web.Features.Rentals.Domain;

namespace RentAgency.Web.Features;

/// <summary>
/// Main facade over the bounded contexts: a single concrete class that exposes
/// the primary domain operations of the agency without exposing service interfaces.
/// </summary>
public sealed class RentAgencySystem(
    IListingService listings,
    ISearchService search,
    IRequestService requests,
    IRentalService rentals,
    IComplaintService complaints)
{
    // Housing: listing lifecycle and moderation
    public Task<Guid> SubmitListingAsync(Guid ownerId, ListingDraft draft, CancellationToken cancellationToken = default)
        => listings.SubmitAsync(ownerId, draft, cancellationToken);

    public Task ResubmitListingAsync(Guid ownerId, Guid listingId, ListingDraft draft, int expectedVersion, CancellationToken cancellationToken = default)
        => listings.ResubmitAsync(ownerId, listingId, draft, expectedVersion, cancellationToken);

    public Task<ModerationOutcome> ModerateListingAsync(Guid moderatorId, Guid listingId, ModerationResult result, string? remarks, int expectedVersion, CancellationToken cancellationToken = default)
        => listings.ModerateAsync(moderatorId, listingId, result, remarks, expectedVersion, cancellationToken);

    // Housing: public search
    public Task<IReadOnlyList<ListingCard>> SearchAsync(SearchCriteria criteria, CancellationToken cancellationToken = default)
        => search.SearchAsync(criteria, cancellationToken);

    public Task<OwnerListingDetails?> GetPublishedListingAsync(Guid listingId, CancellationToken cancellationToken = default)
        => search.GetPublishedListingAsync(listingId, cancellationToken);

    // Rentals: request and discussion
    public Task<Guid> SubmitRequestAsync(Guid tenantId, Guid listingId, CreateRentalRequest request, CancellationToken cancellationToken = default)
        => requests.SubmitAsync(tenantId, listingId, request, cancellationToken);

    public Task StartDiscussionAsync(Guid requestId, Guid ownerId, CancellationToken cancellationToken = default)
        => requests.StartDiscussionAsync(requestId, ownerId, cancellationToken);

    public Task RejectRequestAsync(Guid requestId, Guid ownerId, string reason, CancellationToken cancellationToken = default)
        => requests.RejectAsync(requestId, ownerId, reason, cancellationToken);

    public Task SendMessageAsync(Guid requestId, Guid authorId, string text, CancellationToken cancellationToken = default)
        => requests.SendMessageAsync(requestId, authorId, text, cancellationToken);

    // Rentals: agreement, completion, reviews
    public Task<Guid> ProposeTermsAsync(Guid requestId, Guid participantId, RentalTerms terms, CancellationToken cancellationToken = default)
        => requests.ProposeAgreementAsync(requestId, participantId, terms, cancellationToken);

    public Task ConfirmTermsAsync(Guid agreementId, Guid participantId, int expectedTermsVersion, CancellationToken cancellationToken = default)
        => requests.ConfirmAgreementAsync(agreementId, participantId, expectedTermsVersion, cancellationToken);

    public Task ConfirmCompletionAsync(Guid agreementId, Guid participantId, CancellationToken cancellationToken = default)
        => rentals.ConfirmCompletionAsync(agreementId, participantId, cancellationToken);

    public Task<Review> LeaveReviewAsync(Guid agreementId, Guid authorId, Guid recipientId, string text, CancellationToken cancellationToken = default)
        => rentals.LeaveReviewAsync(agreementId, authorId, recipientId, text, cancellationToken);

    // Complaints
    public Task<Guid> SubmitComplaintAsync(Guid reporterId, ComplaintTarget target, string description, IReadOnlyList<UploadInput> evidence, CancellationToken cancellationToken = default)
        => complaints.SubmitAsync(reporterId, target, description, evidence, cancellationToken);

    public Task RequestComplaintDetailsAsync(Guid moderatorId, Guid complaintId, string details, CancellationToken cancellationToken = default)
        => complaints.RequestDetailsAsync(moderatorId, complaintId, details, cancellationToken);

    public Task ResolveComplaintAsync(Guid moderatorId, Guid complaintId, string decision, string? rationale, CancellationToken cancellationToken = default)
        => complaints.ResolveAsync(moderatorId, complaintId, decision, rationale, cancellationToken);
}
