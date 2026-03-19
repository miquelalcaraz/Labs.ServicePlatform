using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QltS.Identity.Infrastructure.Migrations.IdentityLogs
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identityLogs");

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                schema: "identityLogs",
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
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.UniqueConstraint("AK_AuditLogs_AggregateId", x => x.AggregateId);
                });

            migrationBuilder.CreateTable(
                name: "ClientRequests",
                schema: "identityLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Time = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationEventLog",
                schema: "identityLogs",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventTypeName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    TimesSent = table.Column<int>(type: "int", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationEventLog", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "ProcessedIntegrationEvents",
                schema: "identityLogs",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HandlerName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedIntegrationEvents", x => new { x.EventId, x.HandlerName });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientRequests_Id",
                schema: "identityLogs",
                table: "ClientRequests",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEventLog_State_CreationTime",
                schema: "identityLogs",
                table: "IntegrationEventLog",
                columns: new[] { "State", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEventLog_TransactionId",
                schema: "identityLogs",
                table: "IntegrationEventLog",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedIntegrationEvents_EventId",
                schema: "identityLogs",
                table: "ProcessedIntegrationEvents",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedIntegrationEvents_ProcessedAt",
                schema: "identityLogs",
                table: "ProcessedIntegrationEvents",
                column: "ProcessedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs",
                schema: "identityLogs");

            migrationBuilder.DropTable(
                name: "ClientRequests",
                schema: "identityLogs");

            migrationBuilder.DropTable(
                name: "IntegrationEventLog",
                schema: "identityLogs");

            migrationBuilder.DropTable(
                name: "ProcessedIntegrationEvents",
                schema: "identityLogs");
        }
    }
}
