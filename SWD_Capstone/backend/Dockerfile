FROM mcr.microsoft.com/dotnet/sdk:9.0 AS backend-build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/CPMS.Domain/*.csproj src/CPMS.Domain/
COPY src/CPMS.Shared/*.csproj src/CPMS.Shared/
COPY src/CPMS.Application/*.csproj src/CPMS.Application/
COPY src/CPMS.Infrastructure/*.csproj src/CPMS.Infrastructure/
COPY src/CPMS.API/*.csproj src/CPMS.API/
RUN dotnet restore "src/CPMS.API/CPMS.API.csproj"

COPY src/ src/
RUN dotnet publish "src/CPMS.API/CPMS.API.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

EXPOSE 8080

COPY --from=backend-build /app/publish ./
ENTRYPOINT ["sh", "-c", "dotnet CPMS.API.dll --urls http://0.0.0.0:${PORT:-8080}"]
