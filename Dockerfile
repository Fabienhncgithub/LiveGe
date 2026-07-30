FROM node:22-alpine AS frontend-build
WORKDIR /src/frontend
RUN corepack enable
COPY frontend/package.json frontend/pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile
COPY frontend/ ./
RUN pnpm build

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS backend-build
WORKDIR /src
COPY Directory.Build.props FrontiereLiveGe.slnx dotnet-tools.json ./
COPY backend/FrontiereLiveGe.Api.csproj backend/packages.lock.json ./backend/
RUN dotnet restore backend/FrontiereLiveGe.Api.csproj --locked-mode
COPY backend/ ./backend/
RUN dotnet publish backend/FrontiereLiveGe.Api.csproj -c Release -o /out --no-restore
COPY --from=frontend-build /src/frontend/dist/ /out/wwwroot/

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app
RUN addgroup -S app && adduser -S -G app app \
    && mkdir -p /app/data \
    && chown -R app:app /app
COPY --from=backend-build --chown=app:app /out/ ./
USER app
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:8000
EXPOSE 8000
ENTRYPOINT ["dotnet", "FrontiereLiveGe.Api.dll"]
