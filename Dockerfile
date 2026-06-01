FROM node:22-alpine AS frontend-build
WORKDIR /src/SWD_Capstone/frontend

COPY SWD_Capstone/frontend/package*.json ./
RUN npm ci

COPY SWD_Capstone/frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /src

COPY SWD_Capstone/Directory.Build.props SWD_Capstone/
COPY SWD_Capstone/backend/DataAccessLayer/*.csproj SWD_Capstone/backend/DataAccessLayer/
COPY SWD_Capstone/backend/ServiceLayer/*.csproj SWD_Capstone/backend/ServiceLayer/
COPY SWD_Capstone/backend/PresentationLayer/*.csproj SWD_Capstone/backend/PresentationLayer/
RUN dotnet restore "SWD_Capstone/backend/PresentationLayer/PresentationLayer.csproj"

COPY SWD_Capstone/ SWD_Capstone/
COPY --from=frontend-build /src/SWD_Capstone/frontend/dist/ SWD_Capstone/backend/PresentationLayer/wwwroot/
RUN dotnet publish "SWD_Capstone/backend/PresentationLayer/PresentationLayer.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

COPY --from=backend-build /app/publish ./
ENTRYPOINT ["sh", "-c", "dotnet PresentationLayer.dll --urls http://0.0.0.0:${PORT:-8080}"]
