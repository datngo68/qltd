# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY QuanLyAnTrua/QuanLyAnTrua.csproj QuanLyAnTrua/
RUN dotnet restore QuanLyAnTrua/QuanLyAnTrua.csproj

# Copy everything else and build
COPY . .
WORKDIR /src/QuanLyAnTrua
RUN dotnet build -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install required libraries for System.Drawing (for QR code generation)
RUN apt-get update && \
    apt-get install -y libgdiplus && \
    rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=publish /app/publish .

# Create directories for data persistence
RUN mkdir -p /app/data /app/logs /app/wwwroot/avatars

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5000

# Expose port
EXPOSE 5000

# Run the app
ENTRYPOINT ["dotnet", "QuanLyAnTrua.dll"]

