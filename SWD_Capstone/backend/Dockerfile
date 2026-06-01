FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /src

COPY Directory.Build.props ./
COPY backend/DataAccessLayer/*.csproj backend/DataAccessLayer/
COPY backend/ServiceLayer/*.csproj backend/ServiceLayer/
COPY backend/PresentationLayer/*.csproj backend/PresentationLayer/
RUN dotnet restore "backend/PresentationLayer/PresentationLayer.csproj"

COPY backend/ backend/
RUN dotnet publish "backend/PresentationLayer/PresentationLayer.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

COPY --from=backend-build /app/publish ./
ENTRYPOINT ["sh", "-c", "dotnet PresentationLayer.dll --urls http://0.0.0.0:${PORT:-8080}"]
