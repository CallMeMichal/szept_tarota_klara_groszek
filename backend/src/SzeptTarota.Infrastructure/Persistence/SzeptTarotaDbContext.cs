using Microsoft.EntityFrameworkCore;

namespace SzeptTarota.Infrastructure.Persistence;

public sealed class SzeptTarotaDbContext(DbContextOptions<SzeptTarotaDbContext> options) : DbContext(options);
