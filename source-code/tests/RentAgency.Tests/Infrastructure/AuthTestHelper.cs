using System.Net;
using System.Text.RegularExpressions;

namespace RentAgency.Tests.Infrastructure;

public static class AuthTestHelper
{
    public const string Password = "Password1!";

    public static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");

        Assert.True(match.Success, $"Antiforgery token not found on {url}");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    public static async Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        string email,
        string? extraFieldName = null,
        string? extraFieldValue = null)
    {
        var token = await GetAntiforgeryTokenAsync(client, "/Account/Register");

        var fields = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Name"] = "Тестовый пользователь",
            ["ContactDetails"] = "+7 900 000-00-00",
            ["Email"] = email,
            ["Password"] = Password,
            ["ConfirmPassword"] = Password
        };

        if (extraFieldName is not null)
        {
            fields[extraFieldName] = extraFieldValue ?? string.Empty;
        }

        return await client.PostAsync("/Account/Register", new FormUrlEncodedContent(fields));
    }

    public static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email)
    {
        var token = await GetAntiforgeryTokenAsync(client, "/Account/Login");

        return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Email"] = email,
            ["Password"] = Password
        }));
    }
}
