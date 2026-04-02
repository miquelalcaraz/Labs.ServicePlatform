using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qlt.Infrastructure.Identity.Infrastructure.Migrations.IdentityLogs
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "infra");

            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "IdempotencyEntry",
                schema: "infra",
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
                name: "Logs",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AggregateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AggregateType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Event = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Logs", x => x.Id);
                    table.UniqueConstraint("AK_Logs_AggregateId", x => x.AggregateId);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                schema: "infra",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventTypeName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    TimesSent = table.Column<int>(type: "int", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessage", x => x.EventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyEntry_Id_Scope",
                schema: "infra",
                table: "IdempotencyEntry",
                columns: new[] { "Id", "Scope" });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyEntry_ProcessedAt",
                schema: "infra",
                table: "IdempotencyEntry",
                column: "ProcessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_State_CreationTime",
                schema: "infra",
                table: "OutboxMessage",
                columns: new[] { "State", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_TransactionId",
                schema: "infra",
                table: "OutboxMessage",
                column: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotencyEntry",
                schema: "infra");

            migrationBuilder.DropTable(
                name: "Logs",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "OutboxMessage",
                schema: "infra");
        }
    }
}
