using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SzeptTarota.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderAppointments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "order_appointments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    LineIndex = table.Column<int>(type: "integer", nullable: false),
                    Start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    End = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    HeldUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CalendarEventId = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    MeetLink = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_appointments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_appointments_OrderId",
                table: "order_appointments",
                column: "OrderId");

            // Terminy z Zamówień sprzed blokad zajmują swój czas tak samo jak nowe: opłacone na stałe, a te w trakcie
            // płatności jako blokady od chwili rozpoczęcia płatności (już wygasłe od razu zwolnione).
            migrationBuilder.Sql(
                """
                INSERT INTO order_appointments ("Id", "OrderId", "AccountId", "LineIndex", "Start", "End", "HeldUntil", "ReleasedAt")
                SELECT gen_random_uuid(), o."Id", o."AccountId", (line.position - 1)::integer,
                       (line.value->>'AppointmentStart')::timestamptz, (line.value->>'AppointmentEnd')::timestamptz,
                       CASE WHEN o."Status" = 'Paid' THEN NULL ELSE o."CreatedAt" + interval '15 minutes' END,
                       CASE WHEN o."Status" <> 'Paid' AND o."CreatedAt" + interval '15 minutes' <= now() THEN now() END
                FROM orders o
                CROSS JOIN LATERAL jsonb_array_elements(o."Lines") WITH ORDINALITY AS line(value, position)
                WHERE o."Status" IN ('Paid', 'PendingPayment') AND line.value->>'AppointmentStart' IS NOT NULL AND line.value->>'AppointmentEnd' IS NOT NULL;
                """);

            // Zanim powstało ograniczenie, ten sam czas dało się sprzedać dwa razy. Zostaje Termin opłacony przed
            // blokadą, a z równych ten z wcześniejszego Zamówienia; pozostałe są zwolnione.
            migrationBuilder.Sql(
                """
                UPDATE order_appointments a SET "ReleasedAt" = now()
                FROM orders ao
                WHERE ao."Id" = a."OrderId" AND a."ReleasedAt" IS NULL AND EXISTS (
                    SELECT 1 FROM order_appointments b JOIN orders bo ON bo."Id" = b."OrderId"
                    WHERE b."Id" <> a."Id" AND b."ReleasedAt" IS NULL
                      AND tstzrange(b."Start", b."End") && tstzrange(a."Start", a."End")
                      AND ((b."HeldUntil" IS NULL AND a."HeldUntil" IS NOT NULL)
                           OR ((b."HeldUntil" IS NULL) = (a."HeldUntil" IS NULL)
                               AND (bo."CreatedAt", b."Id") < (ao."CreatedAt", a."Id"))));
                """);

            // Dwa niezwolnione Terminy (opłacone albo zablokowane) nie mogą na siebie nachodzić; stykać się mogą.
            migrationBuilder.Sql(
                """
                ALTER TABLE order_appointments ADD CONSTRAINT "EX_order_appointments_no_overlap"
                EXCLUDE USING gist (tstzrange("Start", "End") WITH &&) WHERE ("ReleasedAt" IS NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_appointments");
        }
    }
}
