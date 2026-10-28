using SzeptTarota.Domain.Accounts;

namespace SzeptTarota.Application.Accounts;

/// <summary>Konta i linki logowania w bazie.</summary>
public interface IAccountStore
{
    Task<Account?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Konto o tym adresie; zakłada je, jeśli jeszcze nie istnieje (także przy dwóch logowaniach naraz).</summary>
    Task<Account> GetOrCreateAsync(string email, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Ile linków logowania wysłano na adres po <paramref name="since"/>.</summary>
    Task<int> CountLoginLinksSinceAsync(string email, DateTimeOffset since, CancellationToken cancellationToken);

    Task AddLoginLinkAsync(LoginLink link, CancellationToken cancellationToken);

    /// <summary>
    /// Oznacza link jako użyty i go zwraca, o ile nie był jeszcze użyty i nie wygasł. Atomowo, więc
    /// dwa równoczesne kliknięcia tego samego linku logują najwyżej raz.
    /// </summary>
    Task<LoginLink?> UseLoginLinkAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken);
}
