rem https://docs.microsoft.com/es-es/ef/core/cli/dotnet

dotnet ef migrations add Initial -c ApplicationIdentityDbContext -o  Data/Migrations/Identity 
dotnet ef migrations add Initial -c QltBackOfficeContextContext -o  Data/Migrations/QltBackOffice
