FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/aabu_reports.webapi/aabu_reports.webapi.csproj", "src/aabu_reports.webapi/"]
COPY ["src/aabu_reports.infrastructure/aabu_reports.infrastructure.csproj", "src/aabu_reports.infrastructure/"]
COPY ["src/aabu_reports.core/aabu_reports.core.csproj", "src/aabu_reports.core/"]
RUN dotnet restore "src/aabu_reports.webapi/aabu_reports.webapi.csproj"
COPY . .
WORKDIR "./src/aabu_reports.webapi"
RUN dotnet build "./aabu_reports.webapi.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./aabu_reports.webapi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "aabu_reports.webapi.dll"]
