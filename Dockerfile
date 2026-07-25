FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src/MafiaOnline

COPY src/MafiaOnline/Api/Api.csproj Api/
COPY src/MafiaOnline/MafiaOnline.Engine/MafiaOnline.Engine.csproj MafiaOnline.Engine/

RUN dotnet restore Api/Api.csproj

COPY . .

WORKDIR /src/MafiaOnline/Api
RUN dotnet publish Api.csproj -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Api.dll"]