FROM mcr.microsoft.com/dotnet/sdk:9.0 AS backend-build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/CPMS.BLL/*.csproj src/CPMS.BLL/
COPY src/CPMS.DAL/*.csproj src/CPMS.DAL/
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
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
ENTRYPOINT ["dotnet", "CPMS.API.dll"]
