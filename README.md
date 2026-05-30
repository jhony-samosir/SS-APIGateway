# SS-APIGateway

## Overview

The `SS-APIGateway` is an enterprise-grade API Gateway built with YARP (Yet Another Reverse Proxy) and ASP.NET Core 10. It serves as the single entry point for the downstream microservices in the SamStore e-commerce platform.

It is designed to orchestrate incoming requests from client applications, validate authorization, ensure traffic resilience, protect downstream services via Zero-Trust policies, and provide unified logging, metrics, and tracing telemetry.

## Features

- **Zero Trust Security Model**: Validates RS256 JWT tokens and injects parsed identity properties into downstream headers while stripping spoofable headers.
- **Internal Origin Signature**: HMAC-SHA256 signs all requests forwarded to internal microservices to verify they originated from the gateway.
- **Polly Resilience Integration**: Integrated with retry, timeout, and circuit breaker policies to ensure robust communication.
- **Observability**: Exposes OpenTelemetry tracing, metrics, and logging, and propagates correlation IDs.
- **Traffic Management**: Implements global and brute-force rate-limiting policies alongside comprehensive security headers.

## Tech Stack

| Category      | Technology                       |
| ------------- | -------------------------------- |
| Backend       | .NET 10.0 (C#)                   |
| Reverse Proxy | YARP (Yet Another Reverse Proxy) |
| Resilience    | Polly                            |
| Telemetry     | OpenTelemetry, Serilog           |

## Project Structure

```text
SS-APIGateway/
├── src/
│   └── SS.APIGateway/         # Main ASP.NET Core YARP Gateway application
├── tests/
│   └── SS.APIGateway.Tests/   # Unit tests for transforms, middleware, and resilience policies
├── docker-compose.yml         # Container orchestrator config for Gateway and microservices
└── SS.APIGateway.slnx         # Solution XML configuration
```

## Requirements

- .NET 10.0 SDK
- Docker and Docker Compose
- RSA Public/Private Key pairs for JWT (stored under `secrets/`)

## Installation

```bash
git clone <repository>
cd SamStore/SS-APIGateway
```

Ensure you have a JWT public key at `secrets/jwt_public_key.pem` relative to the project.

## Configuration

Daftar environment variable yang ditemukan:

```env
GATEWAY_HMAC_SECRET=      # Shared secret key used for HMAC-SHA256 signing of downstream requests
Jwt__PublicKeyPath=       # Path to the JWT RSA public key pem file (e.g. secrets/jwt_public_key.pem)
```

## Running Locally

### Using .NET CLI

```bash
dotnet run --project src/SS.APIGateway
```

### Using Docker Compose

```bash
docker-compose up --build
```

## Build

```bash
dotnet build src/SS.APIGateway/SS.APIGateway.csproj
```

## Testing

```bash
dotnet test
```

## API Documentation

Not identified from source code (API Gateway functions strictly as an intelligent reverse proxy routing requests dynamically).

## Database

Not identified from source code (Stateless gateway service, does not require a database connection).

## Deployment

- **Docker**: Containerized deployment using the multi-stage `Dockerfile`.
- **Docker Compose**: Service definitions orchestrating gateway dependencies and downstream services via `docker-compose.yml`.

## Architecture Notes

- **Gateway Routing Pattern**: Reverse-proxies traffic dynamically using YARP.
- **Zero Trust Policy**: Binds authorization checks on the perimeter and signs downstream headers with HMAC origin verification.
- **Layered Infrastructure**: Contains standard YARP pipeline, custom middleware for response security headers, and YARP Request Transforms.

## Known Issues

Not identified from source code.

## Future Improvements

- Add dynamic routing configuration reload using a backing configuration database or service discovery.
- Integrate distributed rate limiting with a Redis backend.

## License

```text
License information not specified.
```
