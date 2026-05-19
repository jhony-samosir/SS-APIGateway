# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
 
# Copy project files first to leverage Docker cache for restore
COPY ["src/SS.APIGateway/SS.APIGateway.csproj", "src/SS.APIGateway/"]
RUN dotnet restore "src/SS.APIGateway/SS.APIGateway.csproj"

# Copy the rest of the source code
COPY . .
WORKDIR "/src/src/SS.APIGateway"

# Build and publish
RUN dotnet publish "SS.APIGateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Use the built-in non-root 'app' user (available since .NET 8)
# This is more portable and follows Microsoft best practices
USER app

# Copy published artifacts from build stage
COPY --from=build /app/publish .

# Expose the default ASP.NET port
EXPOSE 8080

# Runtime configuration via environment variables:
# - Jwt__PublicKeyPath: Path to RSA public key for JWT validation
# - GATEWAY_HMAC_SECRET: Secret key for signing internal headers
# - Cors__AllowedOrigins__0: First allowed CORS origin
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "SS.APIGateway.dll"]
