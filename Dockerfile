FROM node:22-bookworm-slim AS frontend
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS backend
WORKDIR /src
COPY backend/Offers.Api/Offers.Api.csproj backend/Offers.Api/
RUN dotnet restore backend/Offers.Api/Offers.Api.csproj
COPY backend/Offers.Api/ backend/Offers.Api/
RUN dotnet publish backend/Offers.Api/Offers.Api.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS dotnet-runtime

FROM mcr.microsoft.com/playwright/dotnet:v1.63.0-noble AS runtime
COPY --from=dotnet-runtime /usr/share/dotnet/ /usr/share/dotnet/
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_ROOT=/usr/share/dotnet \
    PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
WORKDIR /app
COPY --from=backend /out/ ./
COPY --from=frontend /src/frontend/dist/ ./wwwroot/
COPY deployment/start.sh ./start.sh
RUN useradd --create-home --uid 10001 offers \
    && mkdir -p /app/.local/keys \
    && chown -R offers:offers /app/.local \
    && chmod +x /app/start.sh
USER offers
EXPOSE 8080
ENTRYPOINT ["/app/start.sh"]
