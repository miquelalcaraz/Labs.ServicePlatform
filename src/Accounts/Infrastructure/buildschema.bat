rem https://docs.microsoft.com/es-es/ef/core/cli/dotnet

rem rmdirXX /S /Q "Data/Migrations"

rem dotnet ef migrations add Initial -c QltSystemDBContext -o  Persistence/Migrations

dotnet ef migrations add Initial -c AccountContext -o  Persistence/Migrations --startup-project ../Api/ 

