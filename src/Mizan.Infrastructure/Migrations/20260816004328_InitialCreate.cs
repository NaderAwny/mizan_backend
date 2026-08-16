using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "otp_codes",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    code = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    attempts_count = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    is_used = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_otp_codes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    first_name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    last_name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    user_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "customer"),
                    password_hash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contacts",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    owner_user_id = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    phone_number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contacts", x => x.id);
                    table.ForeignKey(
                        name: "FK_contacts_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    token = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    replaced_by_token = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shops",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    owner_id = table.Column<int>(type: "int", nullable: false),
                    shop_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    address = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shops", x => x.id);
                    table.ForeignKey(
                        name: "FK_shops_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    owner_user_id = table.Column<int>(type: "int", nullable: false),
                    contact_id = table.Column<int>(type: "int", nullable: false),
                    type = table.Column<int>(type: "int", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    transaction_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    is_installment = table.Column<bool>(type: "bit", nullable: false),
                    installment_plan_mode = table.Column<int>(type: "int", nullable: true),
                    note_type = table.Column<int>(type: "int", nullable: false),
                    note_text = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    note_audio_path = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_transactions_contacts_contact_id",
                        column: x => x.contact_id,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transactions_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "installments",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    transaction_id = table.Column<int>(type: "int", nullable: false),
                    installment_number = table.Column<int>(type: "int", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    due_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    paid_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_installments", x => x.id);
                    table.ForeignKey(
                        name: "FK_installments_transactions_transaction_id",
                        column: x => x.transaction_id,
                        principalTable: "transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "installment_reminder_logs",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    installment_id = table.Column<int>(type: "int", nullable: false),
                    days_before_due = table.Column<int>(type: "int", nullable: false),
                    sent_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_installment_reminder_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_installment_reminder_logs_installments_installment_id",
                        column: x => x.installment_id,
                        principalTable: "installments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    owner_user_id = table.Column<int>(type: "int", nullable: false),
                    type = table.Column<int>(type: "int", nullable: false),
                    title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    transaction_id = table.Column<int>(type: "int", nullable: true),
                    installment_id = table.Column<int>(type: "int", nullable: true),
                    is_read = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_notifications_installments_installment_id",
                        column: x => x.installment_id,
                        principalTable: "installments",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_notifications_transactions_transaction_id",
                        column: x => x.transaction_id,
                        principalTable: "transactions",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_notifications_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contacts_owner_user_id_name",
                table: "contacts",
                columns: new[] { "owner_user_id", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_installment_reminder_logs_installment_id_days_before_due",
                table: "installment_reminder_logs",
                columns: new[] { "installment_id", "days_before_due" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_installments_due_date",
                table: "installments",
                column: "due_date");

            migrationBuilder.CreateIndex(
                name: "IX_installments_transaction_id_installment_number",
                table: "installments",
                columns: new[] { "transaction_id", "installment_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_installment_id",
                table: "notifications",
                column: "installment_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_owner_user_id_created_at",
                table: "notifications",
                columns: new[] { "owner_user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_owner_user_id_is_read",
                table: "notifications",
                columns: new[] { "owner_user_id", "is_read" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_transaction_id",
                table: "notifications",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_otp_codes_email",
                table: "otp_codes",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_token",
                table: "refresh_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_shops_owner_id",
                table: "shops",
                column: "owner_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transactions_contact_id",
                table: "transactions",
                column: "contact_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_owner_user_id_transaction_date",
                table: "transactions",
                columns: new[] { "owner_user_id", "transaction_date" });

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "installment_reminder_logs");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "otp_codes");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "shops");

            migrationBuilder.DropTable(
                name: "installments");

            migrationBuilder.DropTable(
                name: "transactions");

            migrationBuilder.DropTable(
                name: "contacts");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
