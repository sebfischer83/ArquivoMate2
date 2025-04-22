# Unter https://aka.ms/customizecontainer erfahren Sie, wie Sie Ihren Debugcontainer anpassen und wie Visual Studio dieses Dockerfile verwendet, um Ihre Images für ein schnelleres Debuggen zu erstellen.

# Diese Stufe wird verwendet, wenn sie von VS im Schnellmodus ausgeführt wird (Standardeinstellung für Debugkonfiguration).
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# Diese Stufe wird zum Erstellen des Dienstprojekts verwendet.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/ArquivoMate2.API/ArquivoMate2.API.csproj", "src/ArquivoMate2.API/"]
COPY ["src/ArquivoMate2.Application/ArquivoMate2.Application.csproj", "src/ArquivoMate2.Application/"]
COPY ["src/ArquivoMate2.Domain/ArquivoMate2.Domain.csproj", "src/ArquivoMate2.Domain/"]
COPY ["src/ArquivoMate2.Infrastructure/ArquivoMate2.Infrastructure.csproj", "src/ArquivoMate2.Infrastructure/"]
COPY ["src/ArquivoMate2.Shared/ArquivoMate2.Shared.csproj", "src/ArquivoMate2.Shared/"]
RUN dotnet restore "./src/ArquivoMate2.API/ArquivoMate2.API.csproj"
COPY . .
WORKDIR "/src/src/ArquivoMate2.API"
RUN dotnet build "./ArquivoMate2.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Diese Stufe wird verwendet, um das Dienstprojekt zu veröffentlichen, das in die letzte Phase kopiert werden soll.
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ArquivoMate2.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Diese Stufe wird in der Produktion oder bei Ausführung von VS im regulären Modus verwendet (Standard, wenn die Debugkonfiguration nicht verwendet wird).
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ArquivoMate2.API.dll"]