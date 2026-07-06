FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY HubPessoal.Domain/HubPessoal.Domain.csproj HubPessoal.Domain/
COPY HubPessoal.Application/HubPessoal.Application.csproj HubPessoal.Application/
COPY HubPessoal.Infrastructure/HubPessoal.Infrastructure.csproj HubPessoal.Infrastructure/
COPY HubPessoal.Api/HubPessoal.Api.csproj HubPessoal.Api/
RUN dotnet restore HubPessoal.Api/HubPessoal.Api.csproj

COPY . .
RUN dotnet publish HubPessoal.Api/HubPessoal.Api.csproj -c Release -o /app/publish /P:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HubPessoal.Api.dll"]