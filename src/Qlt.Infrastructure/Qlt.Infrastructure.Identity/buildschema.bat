rem https://docs.microsoft.com/es-es/ef/core/cli/dotnet



dotnet ef migrations add Initial -c PersistedGrantDbContext -o  Infrastructure/Migrations/PersistedGrant
dotnet ef migrations add Initial -c ConfigurationDbContext -o  Infrastructure/Migrations/ConfigurationDb
dotnet ef migrations add Initial -c IdentityContext -o  Infrastructure/Migrations/Identity

dotnet ef migrations add Initial -c IdentityLogsContext -o  Infrastructure/Migrations/IdentityLogs