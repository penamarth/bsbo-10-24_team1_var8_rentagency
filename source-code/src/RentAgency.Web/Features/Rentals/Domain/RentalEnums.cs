namespace RentAgency.Web.Features.Rentals.Domain;

public enum RequestStatus
{
    New,
    InDiscussion,
    Rejected,
    Rented
}

public static class RequestStatusText
{
    public static string ToRussian(this RequestStatus status)
    {
        return status switch
        {
            RequestStatus.New => "Новая",
            RequestStatus.InDiscussion => "В обсуждении",
            RequestStatus.Rejected => "Отклонена",
            RequestStatus.Rented => "Сдано",
            _ => status.ToString()
        };
    }
}

public enum AgreementStatus
{
    Draft,
    Confirmed,
    Completed
}

public enum ConfirmationType
{
    Terms,
    Completion
}
