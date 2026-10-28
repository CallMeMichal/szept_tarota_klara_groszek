using SzeptTarota.Domain.Accounts;

namespace SzeptTarota.Application.Accounts;

/// <summary>Konta i kody logowania w bazie.</summary>
public interface IAccountStore
{
    Task<Account?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Konto o tym adresie; zakłada je, jeśli jeszcze nie istnieje (także przy dwóch logowaniach naraz).</summary>
    Task<Account> GetOrCreateAsync(string email, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Ile kodów logowania wysłano na adres po <paramref name="since"/>.</summary>
    Task<int> CountLoginCodesSinceAsync(string email, DateTimeOffset since, CancellationToken cancellationToken);

    Task AddLoginCodeAsync(LoginCode code, CancellationToken cancellationToken);

    /// <summary>Najnowszy kod wysłany na adres, o ile nie był jeszcze użyty i nie wygasł.</summary>
    Task<LoginCode?> FindNewestActiveLoginCodeAsync(string email, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Oznacza kod jako użyty, o ile nie był jeszcze użyty i ma mniej niż <paramref name="maxFailedAttempts"/> błędnych
    /// prób. Atomowo, więc dwa równoczesne wpisania tego samego kodu logują najwyżej raz.
    /// </summary>
    Task<bool> UseLoginCodeAsync(Guid id, int maxFailedAttempts, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Dolicza błędną próbę wpisania kodu.</summary>
    Task AddFailedLoginAttemptAsync(Guid id, CancellationToken cancellationToken);
}
