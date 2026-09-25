# Build context: backend/WeatherUserActions
#   docker build -t weatheruseractions backend/WeatherUserActions

# ---- Stage 1: build (full SDK, discarded after the build) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, using only the project file, so this layer stays cached until dependencies change.
COPY WeatherUserActions/WeatherUserActions.csproj WeatherUserActions/
# --runtime linux-x64: the container only runs on Linux x64, so skip the native PDF/chart
# libraries for the other ~20 platforms (Windows, macOS, ARM...), each ~20 MB.
RUN dotnet restore WeatherUserActions/WeatherUserActions.csproj --runtime linux-x64

COPY WeatherUserActions/ WeatherUserActions/
RUN dotnet publish WeatherUserActions/WeatherUserActions.csproj \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained false \
    --no-restore \
    --output /app/publish

# EF Core migration bundle: a single executable that applies pending migrations when run.
# Building it does NOT touch any database - it runs later, from the pod's init container,
# inside the cluster where SQL Server is reachable. Version matches the EF Core packages.
RUN dotnet tool install --global dotnet-ef --version 10.0.10
ENV PATH="$PATH:/root/.dotnet/tools"
RUN dotnet ef migrations bundle \
    --project WeatherUserActions/WeatherUserActions.csproj \
    --configuration Release \
    --target-runtime linux-x64 \
    --output /app/publish/efbundle

# ---- Stage 2: runtime (ASP.NET runtime only, much smaller than the SDK) ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# The runtime image ships with no fonts. ScottPlot (charts) and QuestPDF (analytics PDF) render
# text with the system's default font, so install one font set plus fontconfig to locate it.
# Clearing the apt lists afterwards keeps them out of the image.
RUN apt-get update \
    && apt-get install -y --no-install-recommends fontconfig fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*

# Brings both the app and efbundle.
COPY --from=build /app/publish .

# .NET images listen on 8080 by default; HTTPS is terminated at the Ingress, not here.
EXPOSE 8080
ENTRYPOINT ["dotnet", "WeatherUserActions.dll"]
