namespace SzeptTarota.Api.Tests.Infrastructure;

/// <summary>Testy w tej kolekcji współdzielą jedną <see cref="ApiFactory"/> (i jeden kontener bazy) i biegną po kolei.</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}
