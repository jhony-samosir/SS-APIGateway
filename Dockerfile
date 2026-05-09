FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY src/SS.APIGateway/*.csproj ./src/SS.APIGateway/
RUN dotnet restore src/SS.APIGateway/SS.APIGateway.csproj

# Copy everything else and build
COPY . .
WORKDIR /app/src/SS.APIGateway
RUN dotnet publish -c Release -o /app/out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/out .

# Expose port 8080 (default for ASP.NET Core)
EXPOSE 8080
ENTRYPOINT ["dotnet", "SS.APIGateway.dll"]
