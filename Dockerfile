# Stage 1: Build Angular client
FROM node:20-alpine AS client-build
WORKDIR /app/client
COPY BluffGame.Client/package*.json ./
RUN npm ci
COPY BluffGame.Client/ ./
RUN npm run build -- --configuration production

# Stage 2: Build .NET server
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS server-build
WORKDIR /app
COPY BluffGame.Server/*.csproj ./BluffGame.Server/
RUN dotnet restore BluffGame.Server/BluffGame.Server.csproj
COPY BluffGame.Server/ ./BluffGame.Server/
COPY --from=client-build /app/client/dist/bluff-game-client/browser ./BluffGame.Server/wwwroot
RUN dotnet publish BluffGame.Server/BluffGame.Server.csproj -c Release -o /app/publish

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=server-build /app/publish .
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000
ENTRYPOINT ["dotnet", "BluffGame.Server.dll"]
