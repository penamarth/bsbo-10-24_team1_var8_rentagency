using RentAgency.Web.Features.Complaints.Domain;
using RentAgency.Web.Features.Housing.Domain;
using RentAgency.Web.Features.Rentals.Domain;

namespace RentAgency.Web.Infrastructure;

public static class StatusBadge
{
    public static string For(ListingStatus status)
    {
        return status switch
        {
            ListingStatus.OnModeration => "badge badge--accent",
            ListingStatus.NeedsRevision => "badge badge--warn",
            ListingStatus.Rejected => "badge badge--danger",
            ListingStatus.Published => "badge badge--success",
            ListingStatus.Rented => "badge badge--done",
            _ => "badge badge--muted"
        };
    }

    public static string For(RequestStatus status)
    {
        return status switch
        {
            RequestStatus.New => "badge badge--accent",
            RequestStatus.InDiscussion => "badge badge--warn",
            RequestStatus.Rejected => "badge badge--danger",
            RequestStatus.Rented => "badge badge--success",
            _ => "badge badge--muted"
        };
    }

    public static string For(AgreementStatus status)
    {
        return status switch
        {
            AgreementStatus.Draft => "badge badge--muted",
            AgreementStatus.Confirmed => "badge badge--accent",
            AgreementStatus.Completed => "badge badge--success",
            _ => "badge badge--muted"
        };
    }

    public static string For(ComplaintStatus status)
    {
        return status switch
        {
            ComplaintStatus.Open => "badge badge--accent",
            ComplaintStatus.AwaitingReporter => "badge badge--warn",
            ComplaintStatus.Resolved => "badge badge--done",
            _ => "badge badge--muted"
        };
    }
}
