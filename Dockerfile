FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release

RUN apt-get update && apt-get install -y curl \
    && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y nodejs

WORKDIR /src/MafiaOnline

# 1. Copy the project files
COPY src/MafiaOnline/Api/Api.csproj Api/
COPY src/MafiaOnline/MafiaOnline.Engine/MafiaOnline.Engine.csproj MafiaOnline.Engine/

RUN dotnet restore Api/Api.csproj

# 2. Copy the contents of the local MafiaOnline directory to the current WORKDIR
COPY src/MafiaOnline/ .

# 3. Publish
WORKDIR /src/MafiaOnline/Api
RUN dotnet publish Api.csproj -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Api.dll"]