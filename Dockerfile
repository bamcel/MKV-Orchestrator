# syntax=docker/dockerfile:1

# Build MKV Orchestrator inside a Linux .NET SDK image.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore MKVOrchestrator.sln
RUN dotnet publish src/MKVOrchestrator.App/MKVOrchestrator.App.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o /app/publish

# Runtime image for running the Avalonia desktop app in a Linux container.
# GUI containers require host display access, usually X11 or Wayland.
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends \
    libx11-6 \
    libxext6 \
    libxrender1 \
    libxtst6 \
    libxi6 \
    libxrandr2 \
    libxcursor1 \
    libxinerama1 \
    libfontconfig1 \
    libfreetype6 \
    libglib2.0-0 \
    libgtk-3-0 \
    libice6 \
    libsm6 \
    mkvtoolnix \
    ffmpeg \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MKVOrchestrator.App.dll"]
