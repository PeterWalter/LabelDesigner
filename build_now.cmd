@echo off
set NUGET_COMMON_APPLICATION_DATA=C:\ProgramData
set NUGET_PACKAGES=%USERPROFILE%\.nuget\packages
dotnet restore LabelDesigner.Core\LabelDesigner.Core.csproj
dotnet restore LabelDesigner.Application\LabelDesigner.Application.csproj
dotnet restore LabelDesigner.Infrastructure\LabelDesigner.Infrastructure.csproj
dotnet restore LabelDesigner.App\LabelDesigner.App.csproj
dotnet restore LabelDesigner.Tests\LabelDesigner.Tests.csproj
dotnet build LabelDesigner.slnx --configuration Debug
