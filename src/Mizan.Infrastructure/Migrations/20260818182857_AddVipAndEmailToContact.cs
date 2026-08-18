using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVipAndEmailToContact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "contact_email",
                table: "contacts",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_vip",
                table: "contacts",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "contact_email",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "is_vip",
                table: "contacts");
        }
    }
}
