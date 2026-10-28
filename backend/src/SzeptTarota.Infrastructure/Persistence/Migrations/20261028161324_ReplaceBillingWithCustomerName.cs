using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SzeptTarota.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceBillingWithCustomerName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Billing_City",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "Billing_PostalCode",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "Billing_Street",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "Billing_TaxId",
                table: "orders");

            // Imię i nazwisko z danych do faktury zostaje jako imię i nazwisko Klienta.
            migrationBuilder.RenameColumn(
                name: "Billing_Name",
                table: "orders",
                newName: "CustomerName");

            migrationBuilder.AlterColumn<string>(
                name: "CustomerName",
                table: "orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CustomerName",
                table: "orders",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.RenameColumn(
                name: "CustomerName",
                table: "orders",
                newName: "Billing_Name");

            migrationBuilder.AddColumn<string>(
                name: "Billing_City",
                table: "orders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Billing_PostalCode",
                table: "orders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Billing_Street",
                table: "orders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Billing_TaxId",
                table: "orders",
                type: "text",
                nullable: true);
        }
    }
}
