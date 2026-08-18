using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContactEmailSentToReminderLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "contact_email_sent",
                table: "installment_reminder_logs",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "contact_email_sent",
                table: "installment_reminder_logs");
        }
    }
}
