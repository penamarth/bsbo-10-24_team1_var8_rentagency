using RentAgency.Web.Features.Rentals.Domain;

namespace RentAgency.Web.Features.Rentals.Application;

public sealed record CreateRentalRequest(
    string InitialMessage,
    DateOnly ProposedStart,
    DateOnly ProposedEnd);

public sealed record RequestMessageView(Guid Id, Guid AuthorId, string Text, DateTime SentAt);

public sealed record AgreementView(
    Guid AgreementId,
    AgreementStatus Status,
    RentalTerms Terms,
    int TermsVersion,
    IReadOnlyList<Guid> TermsConfirmedBy,
    IReadOnlyList<Guid> CompletionConfirmedBy,
    DateTime? ConfirmedAt,
    DateTime? CompletedAt);

public sealed record RentalRequestView(
    Guid RequestId,
    Guid ListingId,
    Guid OwnerId,
    Guid TenantId,
    string InitialMessage,
    DateOnly ProposedStart,
    DateOnly ProposedEnd,
    RequestStatus Status,
    string? RejectionReason,
    DateTime CreatedAt,
    IReadOnlyList<RequestMessageView> Messages,
    AgreementView? Agreement);

public sealed record OwnerRequestSummary(
    Guid RequestId,
    Guid ListingId,
    string Address,
    Guid TenantId,
    RequestStatus Status,
    DateTime CreatedAt);

public interface IRequestService
{
    Task<Guid> SubmitAsync(
        Guid tenantId,
        Guid listingId,
        CreateRentalRequest request,
        CancellationToken cancellationToken = default);

    Task<RentalRequestView> GetRequestAsync(
        Guid requestId,
        Guid participantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OwnerRequestSummary>> GetOwnerRequestsAsync(
        Guid ownerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OwnerRequestSummary>> GetTenantRequestsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task StartDiscussionAsync(Guid requestId, Guid ownerId, CancellationToken cancellationToken = default);

    Task RejectAsync(Guid requestId, Guid ownerId, string reason, CancellationToken cancellationToken = default);

    Task SendMessageAsync(Guid requestId, Guid authorId, string text, CancellationToken cancellationToken = default);

    Task<Guid> ProposeAgreementAsync(
        Guid requestId,
        Guid participantId,
        RentalTerms terms,
        CancellationToken cancellationToken = default);

    Task ConfirmAgreementAsync(
        Guid agreementId,
        Guid participantId,
        int expectedTermsVersion,
        CancellationToken cancellationToken = default);

    Task<AgreementView> GetAgreementAsync(
        Guid agreementId,
        Guid participantId,
        CancellationToken cancellationToken = default);
}
