using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoiceNotesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "voice_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    shop_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    contact_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    party_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    operation_type = table.Column<int>(type: "int", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    operation_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    audio_path = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voice_notes", x => x.id);
                    table.ForeignKey(
                        name: "FK_voice_notes_contacts_contact_id",
                        column: x => x.contact_id,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_voice_notes_shops_shop_id",
                        column: x => x.shop_id,
                        principalTable: "shops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_voice_notes_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_voice_notes_contact_id",
                table: "voice_notes",
                column: "contact_id");

            migrationBuilder.CreateIndex(
                name: "IX_voice_notes_operation_date",
                table: "voice_notes",
                column: "operation_date");

            migrationBuilder.CreateIndex(
                name: "IX_voice_notes_owner_user_id",
                table: "voice_notes",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_voice_notes_shop_id_is_active",
                table: "voice_notes",
                columns: new[] { "shop_id", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "voice_notes");
        }
    }
}
