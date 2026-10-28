using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SzeptTarota.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuantityToOrderLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Pozycje Zamówień sprzed liczby sztuk to zawsze jedna sztuka.
            migrationBuilder.Sql("""
                UPDATE orders
                SET "Lines" = (
                    SELECT coalesce(jsonb_agg(line || '{"Quantity": 1}'::jsonb ORDER BY position), '[]'::jsonb)
                    FROM jsonb_array_elements("Lines") WITH ORDINALITY AS lines(line, position))
                WHERE EXISTS (
                    SELECT 1 FROM jsonb_array_elements("Lines") AS line WHERE NOT line ? 'Quantity');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE orders
                SET "Lines" = (
                    SELECT coalesce(jsonb_agg(line - 'Quantity' ORDER BY position), '[]'::jsonb)
                    FROM jsonb_array_elements("Lines") WITH ORDINALITY AS lines(line, position));
                """);
        }
    }
}
