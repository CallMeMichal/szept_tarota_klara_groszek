using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SzeptTarota.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CountAllLoginCodeAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FailedAttempts",
                table: "login_codes",
                newName: "Attempts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Attempts",
                table: "login_codes",
                newName: "FailedAttempts");
        }
    }
}
