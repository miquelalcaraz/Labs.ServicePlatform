using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miqa.Infrastructure.Identity.Infrastructure.Migrations.IdentityLogs
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IdempotencyEntry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Qualifier = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: ""),
                    TypeName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyEntry", x => new { x.Id, x.Scope, x.Qualifier });
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TypeName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SendCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessage", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyEntry_Id_Scope",
                table: "IdempotencyEntry",
                columns: new[] { "Id", "Scope" });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyEntry_ProcessedAt",
                table: "IdempotencyEntry",
                column: "ProcessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_Status_CreatedAt",
                table: "OutboxMessage",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_TransactionId",
                table: "OutboxMessage",
                column: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotencyEntry");

            migrationBuilder.DropTable(
                name: "OutboxMessage");
        }
    }
}
