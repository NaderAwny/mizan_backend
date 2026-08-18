using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionsTableUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "contact_id",
                table: "transactions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "party_name",
                table: "transactions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "payment_method",
                table: "transactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "shop_id",
                table: "transactions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_transactions_shop_id_transaction_date",
                table: "transactions",
                columns: new[] { "shop_id", "transaction_date" });

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_shops_shop_id",
                table: "transactions",
                column: "shop_id",
                principalTable: "shops",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transactions_shops_shop_id",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_shop_id_transaction_date",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "party_name",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "payment_method",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "shop_id",
                table: "transactions");

            migrationBuilder.AlterColumn<Guid>(
                name: "contact_id",
                table: "transactions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
