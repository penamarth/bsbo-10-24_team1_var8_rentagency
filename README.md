## Вариант

8.Информационная система жилищного агентства
Обзор: информационная система жилищного агентства позволяет квартиросъемщикам подобрать и снять жилье, а владельцам жилья - предложить и сдать жилье.

Пример: https://gitlab.com/penamarth/pos.git

## Running locally

Requirements: .NET 10 SDK (with the ASP.NET Core runtime).

```bash
dotnet restore
dotnet run --project source-code/src/RentAgency.Web
```

The application uses SQLite (`app.db`) and applies EF Core migrations on startup. Uploaded files are stored outside `wwwroot` in `source-code/src/RentAgency.Web/App_Data/uploads`; listing photos and complaint evidence are served only through access-checked endpoints.

### Moderator account

Moderator credentials are never stored in source control. Provide them through user-secrets or environment variables before the first run:

```bash
dotnet user-secrets set "Identity:Moderator:Email" "moderator@example.com" --project source-code/src/RentAgency.Web
dotnet user-secrets set "Identity:Moderator:Password" "change-me-123" --project source-code/src/RentAgency.Web
dotnet user-secrets set "Identity:Moderator:Name" "Модератор" --project source-code/src/RentAgency.Web
```

Equivalent environment variables: `Identity__Moderator__Email`, `Identity__Moderator__Password`, `Identity__Moderator__Name`. Without them the role is still seeded but no moderator account is created.

### Tests

```bash
dotnet build source-code/RentAgency.sln
dotnet test source-code/tests/RentAgency.Tests/RentAgency.Tests.csproj
```
