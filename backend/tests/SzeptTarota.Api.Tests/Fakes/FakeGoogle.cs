using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace SzeptTarota.Api.Tests.Fakes;

/// <summary>
/// Udaje serwery Google, z którymi API rozmawia po powrocie Klienta z logowania Google: wymienia kod autoryzacji
/// na token i zwraca dane konta Google. Test „loguje się” w Google przez <see cref="SignIn"/> i dostaje kod,
/// z którym Google odesłałby przeglądarkę do API.
/// </summary>
public sealed class FakeGoogle : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, GoogleUser> _users = new();

    /// <summary>Kod autoryzacji, jaki Google wyda po zalogowaniu się na konto Google z adresem <paramref name="email"/>.</summary>
    public string SignIn(string email, bool emailVerified = true)
    {
        var code = $"google-code-{Guid.NewGuid():N}";
        _users[code] = new GoogleUser($"google-{Guid.NewGuid():N}", email, emailVerified);
        return code;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        switch (request.RequestUri!.AbsolutePath)
        {
            case "/token":
                var form = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellationToken));
                return form.TryGetValue("code", out var code) && _users.ContainsKey(code.ToString())
                    ? Json(new { access_token = code.ToString(), token_type = "Bearer", expires_in = 3600 })
                    : new HttpResponseMessage(HttpStatusCode.BadRequest);

            case "/oauth2/v3/userinfo":
                return request.Headers.Authorization?.Parameter is { } token && _users.TryGetValue(token, out var user)
                    ? Json(new { sub = user.Subject, email = user.Email, email_verified = user.EmailVerified })
                    : new HttpResponseMessage(HttpStatusCode.Unauthorized);

            default:
                return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    private sealed record GoogleUser(string Subject, string Email, bool EmailVerified);
}
