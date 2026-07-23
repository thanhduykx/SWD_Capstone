FROM mcr.microsoft.com/dotnet/sdk:9.0 AS backend-build
WORKDIR /src

COPY SWD_Capstone/Directory.Build.props ./
COPY SWD_Capstone/src/CPMS.BLL/*.csproj src/CPMS.BLL/
COPY SWD_Capstone/src/CPMS.DAL/*.csproj src/CPMS.DAL/
COPY SWD_Capstone/src/CPMS.API/*.csproj src/CPMS.API/
RUN dotnet restore "src/CPMS.API/CPMS.API.csproj"

COPY SWD_Capstone/src/ src/
RUN dotnet publish "src/CPMS.API/CPMS.API.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

EXPOSE 8080

COPY --from=backend-build /app/publish ./
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
ENTRYPOINT ["dotnet", "CPMS.API.dll"]
