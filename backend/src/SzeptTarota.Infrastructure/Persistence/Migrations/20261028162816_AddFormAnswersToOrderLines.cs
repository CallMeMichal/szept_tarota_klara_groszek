using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SzeptTarota.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFormAnswersToOrderLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Pozycje Zamówień sprzed formularzy Usług dostają pustą listę odpowiedzi.
            migrationBuilder.Sql("""
                UPDATE orders
                SET "Lines" = (
                    SELECT coalesce(jsonb_agg(line || '{"FormAnswers": []}'::jsonb ORDER BY position), '[]'::jsonb)
                    FROM jsonb_array_elements("Lines") WITH ORDINALITY AS lines(line, position))
                WHERE EXISTS (
                    SELECT 1 FROM jsonb_array_elements("Lines") AS line WHERE NOT line ? 'FormAnswers');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE orders
                SET "Lines" = (
                    SELECT coalesce(jsonb_agg(line - 'FormAnswers' ORDER BY position), '[]'::jsonb)
                    FROM jsonb_array_elements("Lines") WITH ORDINALITY AS lines(line, position));
                """);
        }
    }
}
