# ─────────────────────────────────────────────
# Stage 1: Build Angular UI
# ─────────────────────────────────────────────
FROM node:22-alpine AS ui-build

WORKDIR /app/ui
COPY Proxy.UI/package*.json ./
RUN npm ci --silent

COPY Proxy.UI/ ./
RUN npm run build -- --configuration production

# ─────────────────────────────────────────────
# Stage 2: Build .NET API
# ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build

WORKDIR /app/api
COPY Proxy.Host/*.csproj ./
RUN dotnet restore

COPY Proxy.Host/ ./

# Copy Angular build output into wwwroot
# outputPath in angular.json = dist/app → browser assets are in dist/app/browser
COPY --from=ui-build /app/ui/dist/app/browser ./wwwroot

RUN dotnet publish -c Release -o /app/publish

# ─────────────────────────────────────────────
# Stage 3: Final runtime image
# ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

# Copy published API (already contains wwwroot from stage 2)
COPY --from=api-build /app/publish .

# Data directory for LiteDB files — mount a volume here to persist data
RUN mkdir -p /app/data
ENV LiteDb__Path=/app/data/proxy.db
ENV LiteDb__LogPath=/app/data/trafik-proxy-log.db
VOLUME ["/app/data"]

EXPOSE 8080

ENTRYPOINT ["dotnet", "Proxy.Host.dll"]
