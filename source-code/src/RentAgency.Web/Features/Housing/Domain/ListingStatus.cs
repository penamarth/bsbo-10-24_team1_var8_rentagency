namespace RentAgency.Web.Features.Housing.Domain;

public enum ListingStatus
{
    OnModeration,
    NeedsRevision,
    Rejected,
    Published,
    Rented
}

public static class ListingStatusText
{
    public static string ToRussian(this ListingStatus status)
    {
        return status switch
        {
            ListingStatus.OnModeration => "На модерации",
            ListingStatus.NeedsRevision => "На доработке",
            ListingStatus.Rejected => "Отклонено",
            ListingStatus.Published => "Опубликовано",
            ListingStatus.Rented => "Сдано",
            _ => status.ToString()
        };
    }
}

public enum ModerationResult
{
    Approved,
    ReturnedForRevision,
    Rejected
}

public static class ModerationResultText
{
    public static string ToRussian(this ModerationResult result)
    {
        return result switch
        {
            ModerationResult.Approved => "Одобрено",
            ModerationResult.ReturnedForRevision => "На доработку",
            ModerationResult.Rejected => "Отклонено",
            _ => result.ToString()
        };
    }
}
