# Requires access to mcr.microsoft.com, Ubuntu package mirrors and NuGet.
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble

# Native dependencies for Avalonia/X11 and an optional virtual display.
RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates git \
    libice6 libsm6 libfontconfig1 \
    libx11-6 libx11-xcb1 libxcb1 libxcb-render0 libxcb-shm0 libxcb-xfixes0 \
    libxi6 libxrandr2 libxrender1 libxcursor1 libxkbcommon0 \
    libgl1 libegl1 libdbus-1-3 \
    fonts-noto-cjk xvfb xauth \
    && rm -rf /var/lib/apt/lists/*

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1
WORKDIR /workspace
