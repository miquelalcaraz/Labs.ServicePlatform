using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miqa.Services.Accounts.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Accounts");

            migrationBuilder.CreateTable(
                name: "Company",
                schema: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegalName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TradeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CompanyType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Address_Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Address_CountryCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Address_State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Address_PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Address_City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Address_Street = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TaxId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TaxNumberType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedByInternalUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Company", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerActivationProcess",
                schema: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CurrentStep = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    LastErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ActivationRequestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ActivationDeliveredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UserActivatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyLegalName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SubscriptionPlanCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AdminEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastRetryAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerActivationProcess", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyEntry",
                schema: "Accounts",
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
                name: "OnboardingProcess",
                schema: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CurrentStep = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    InitiatedByInternalUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    LastRetryAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailedStep = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CompanyLegalName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    SubscriptionPlanCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AdminEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    AdminFirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AdminLastName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    TenantName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    AdminMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingProcess", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                schema: "Accounts",
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

            migrationBuilder.CreateTable(
                name: "Subscription",
                schema: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PlanCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartDateUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndDateUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedByInternalUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SuspendedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscription", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenant",
                schema: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OwnerCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByInternalUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenant", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserMembership",
                schema: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MembershipType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RoleCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByInternalUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMembership", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompanyContact",
                schema: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    JobTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyContact", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Company_CompanyContacts",
                        column: x => x.CompanyId,
                        principalSchema: "Accounts",
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Company_TenantId",
                schema: "Accounts",
                table: "Company",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyContact_CompanyId",
                schema: "Accounts",
                table: "CompanyContact",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyContact_Email",
                schema: "Accounts",
                table: "CompanyContact",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerActivationProcess_CorrelationId",
                schema: "Accounts",
                table: "CustomerActivationProcess",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerActivationProcess_SubscriptionId",
                schema: "Accounts",
                table: "CustomerActivationProcess",
                column: "SubscriptionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerActivationProcess_TenantId",
                schema: "Accounts",
                table: "CustomerActivationProcess",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyEntry_Id_Scope",
                schema: "Accounts",
                table: "IdempotencyEntry",
                columns: new[] { "Id", "Scope" });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyEntry_ProcessedAt",
                schema: "Accounts",
                table: "IdempotencyEntry",
                column: "ProcessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingProcess_CorrelationId",
                schema: "Accounts",
                table: "OnboardingProcess",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingProcess_SubscriptionId",
                schema: "Accounts",
                table: "OnboardingProcess",
                column: "SubscriptionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingProcess_TenantId",
                schema: "Accounts",
                table: "OnboardingProcess",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_Status_CreatedAt",
                schema: "Accounts",
                table: "OutboxMessage",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_TransactionId",
                schema: "Accounts",
                table: "OutboxMessage",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscription_TenantId",
                schema: "Accounts",
                table: "Subscription",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenant_OwnerCompanyId",
                schema: "Accounts",
                table: "Tenant",
                column: "OwnerCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMembership_CompanyId",
                schema: "Accounts",
                table: "UserMembership",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMembership_TenantId",
                schema: "Accounts",
                table: "UserMembership",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMembership_UserId",
                schema: "Accounts",
                table: "UserMembership",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyContact",
                schema: "Accounts");

            migrationBuilder.DropTable(
                name: "CustomerActivationProcess",
                schema: "Accounts");

            migrationBuilder.DropTable(
                name: "IdempotencyEntry",
                schema: "Accounts");

            migrationBuilder.DropTable(
                name: "OnboardingProcess",
                schema: "Accounts");

            migrationBuilder.DropTable(
                name: "OutboxMessage",
                schema: "Accounts");

            migrationBuilder.DropTable(
                name: "Subscription",
                schema: "Accounts");

            migrationBuilder.DropTable(
                name: "Tenant",
                schema: "Accounts");

            migrationBuilder.DropTable(
                name: "UserMembership",
                schema: "Accounts");

            migrationBuilder.DropTable(
                name: "Company",
                schema: "Accounts");
        }
    }
}
