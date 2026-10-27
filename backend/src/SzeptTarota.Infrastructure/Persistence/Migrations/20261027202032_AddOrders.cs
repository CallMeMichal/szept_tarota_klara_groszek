using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SzeptTarota.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Total = table.Column<int>(type: "integer", nullable: false),
                    PaymentSessionId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NotificationsSentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Billing_City = table.Column<string>(type: "text", nullable: false),
                    Billing_Name = table.Column<string>(type: "text", nullable: false),
                    Billing_PostalCode = table.Column<string>(type: "text", nullable: false),
                    Billing_Street = table.Column<string>(type: "text", nullable: false),
                    Billing_TaxId = table.Column<string>(type: "text", nullable: true),
                    Consents = table.Column<string>(type: "jsonb", nullable: false),
                    Lines = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_orders_PaymentSessionId",
                table: "orders",
                column: "PaymentSessionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "orders");
        }
    }
}
