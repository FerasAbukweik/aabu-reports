FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/AabuReports.Webapi/AabuReports.Webapi.csproj", "src/AabuReports.Webapi/"]
COPY ["src/AabuReports.Infrastructure/AabuReports.Infrastructure.csproj", "src/AabuReports.Infrastructure/"]
COPY ["src/AabuReports.Core/AabuReports.Core.csproj", "src/AabuReports.Core/"]
RUN dotnet restore "src/AabuReports.Webapi/AabuReports.Webapi.csproj"
COPY . .
WORKDIR "./src/AabuReports.Webapi"
RUN dotnet build "./AabuReports.Webapi.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./AabuReports.Webapi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AabuReports.Webapi.dll"]
