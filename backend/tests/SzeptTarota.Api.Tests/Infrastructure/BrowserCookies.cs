using System.Net.Http.Headers;

namespace SzeptTarota.Api.Tests.Infrastructure;

/// <summary>
/// Ciasteczka jak w przeglądarce, ale bez patrzenia na ich datę ważności. API liczy ją z <c>FakeTimeProvider</c>,
/// więc wg prawdziwego zegara ciasteczko sesji bywa już „przeterminowane” i <c>CookieContainer</c> by je odrzucił.
/// Ciasteczko znika, gdy serwer je wyczyści (pusta wartość, jak przy wylogowaniu).
/// </summary>
public sealed class BrowserCookies : DelegatingHandler
{
    private readonly Dictionary<string, string> _cookies = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (_cookies)
        {
            if (_cookies.Count > 0)
            {
                request.Headers.Add("Cookie", string.Join("; ", _cookies.Select(c => $"{c.Key}={c.Value}")));
            }
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            lock (_cookies)
            {
                foreach (var header in setCookies)
                {
                    Store(header);
                }
            }
        }

        return response;
    }

    private void Store(string setCookie)
    {
        var nameValue = setCookie.Split(';', 2)[0];
        var separator = nameValue.IndexOf('=');
        var (name, value) = (nameValue[..separator].Trim(), nameValue[(separator + 1)..].Trim());
        if (value.Length == 0)
        {
            _cookies.Remove(name);
        }
        else
        {
            _cookies[name] = value;
        }
    }
}
