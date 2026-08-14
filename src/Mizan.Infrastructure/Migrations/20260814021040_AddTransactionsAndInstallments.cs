using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionsAndInstallments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<int>(type: "int", nullable: false),
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsInstallment = table.Column<bool>(type: "bit", nullable: false),
                    InstallmentPlanMode = table.Column<int>(type: "int", nullable: true),
                    NoteType = table.Column<int>(type: "int", nullable: false),
                    NoteText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NoteAudioPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_transactions_contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transactions_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "installments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstallmentNumber = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_installments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_installments_transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_installments_DueDate",
                table: "installments",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_installments_TransactionId_InstallmentNumber",
                table: "installments",
                columns: new[] { "TransactionId", "InstallmentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transactions_ContactId",
                table: "transactions",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_OwnerUserId_TransactionDate",
                table: "transactions",
                columns: new[] { "OwnerUserId", "TransactionDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "installments");

            migrationBuilder.DropTable(
                name: "transactions");
        }
    }
}
