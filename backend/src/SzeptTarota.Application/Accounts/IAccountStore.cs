using SzeptTarota.Domain.Accounts;

namespace SzeptTarota.Application.Accounts;

/// <summary>Konta i kody logowania w bazie.</summary>
public interface IAccountStore
{
    Task<Account?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Konto o tym adresie (małymi literami) albo <c>null</c>, gdy go nie ma.</summary>
    Task<Account?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>Konto o tym adresie; zakłada je, jeśli jeszcze nie istnieje (także przy dwóch logowaniach naraz).</summary>
    Task<Account> GetOrCreateAsync(string email, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Ile kodów logowania wysłano na adres po <paramref name="since"/>.</summary>
    Task<int> CountLoginCodesSinceAsync(string email, DateTimeOffset since, CancellationToken cancellationToken);

    Task AddLoginCodeAsync(LoginCode code, CancellationToken cancellationToken);

    /// <summary>Najnowszy kod wysłany na adres, o ile nie był jeszcze użyty i nie wygasł.</summary>
    Task<LoginCode?> FindNewestActiveLoginCodeAsync(string email, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Dolicza próbę wpisania kodu, o ile nie był jeszcze użyty i miał mniej niż <paramref name="maxAttempts"/> prób;
    /// <c>false</c>, gdy prób już nie ma. Atomowo, więc nawet wiele równoczesnych zgadnięć dostaje razem najwyżej
    /// <paramref name="maxAttempts"/> prób.
    /// </summary>
    Task<bool> TryStartLoginAttemptAsync(Guid id, int maxAttempts, CancellationToken cancellationToken);

    /// <summary>Oznacza kod jako użyty, o ile jeszcze nie był; atomowo, więc kod loguje najwyżej raz.</summary>
    Task<bool> UseLoginCodeAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken);
}
