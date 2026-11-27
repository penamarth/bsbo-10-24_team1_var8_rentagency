namespace RentAgency.Web.Features.Accounts;

public static class UserRoles
{
    public const string Tenant = "Tenant";

    public const string Owner = "Owner";

    public const string Moderator = "Moderator";

    public static readonly string[] All = [Tenant, Owner, Moderator];
}
