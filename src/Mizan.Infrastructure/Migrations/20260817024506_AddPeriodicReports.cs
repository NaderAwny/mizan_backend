using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPeriodicReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "periodic_report_id",
                table: "notifications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "periodic_reports",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    owner_user_id = table.Column<int>(type: "int", nullable: false),
                    batch_number = table.Column<int>(type: "int", nullable: false),
                    transaction_count = table.Column<int>(type: "int", nullable: false),
                    total_sales_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    total_purchases_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    pdf_storage_path = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    email_sent = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    generated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_periodic_reports", x => x.id);
                    table.ForeignKey(
                        name: "FK_periodic_reports_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_periodic_report_id",
                table: "notifications",
                column: "periodic_report_id");

            migrationBuilder.CreateIndex(
                name: "IX_periodic_reports_email_sent",
                table: "periodic_reports",
                column: "email_sent");

            migrationBuilder.CreateIndex(
                name: "IX_periodic_reports_owner_user_id_batch_number",
                table: "periodic_reports",
                columns: new[] { "owner_user_id", "batch_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_periodic_reports_owner_user_id_generated_at",
                table: "periodic_reports",
                columns: new[] { "owner_user_id", "generated_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_periodic_reports_periodic_report_id",
                table: "notifications",
                column: "periodic_report_id",
                principalTable: "periodic_reports",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_notifications_periodic_reports_periodic_report_id",
                table: "notifications");

            migrationBuilder.DropTable(
                name: "periodic_reports");

            migrationBuilder.DropIndex(
                name: "IX_notifications_periodic_report_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "periodic_report_id",
                table: "notifications");
        }
    }
}
