# SS.APIGateway

Enterprise API Gateway built with YARP and ASP.NET Core 10.

[**View API Endpoint Matrix & Documentation**](API_MATRIX.md)

## Features

- **Zero Trust Security**:
    - RS256 JWT Validation.
    - Header Spoofing Protection (Strips spoofable headers from clients).
    - Identity Injection (Injects clean `X-User-*` headers from JWT claims).
    - Internal Origin Signature (HMAC-SHA256 signing for downstream requests).
- **Resiliency**:
    - Polly-based policies: Timeout, Retry, and Circuit Breaker.
    - Custom `IForwarderHttpClientFactory` integration.
- **Observability**:
    - OpenTelemetry Tracing, Metrics, and Logging.
    - Correlation ID generation/forwarding.
- **Traffic Management**:
    - Global and Brute-force Rate Limiting policies.
    - CORS and OWASP-recommended Security Headers.

## Project Structure

- `src/SS.APIGateway`: Main application.
- `tests/SS.APIGateway.Tests`: Unit tests for transforms and middleware.

## Running Locally

### Prerequisites
- .NET 10 SDK
- Docker & Docker Compose

### Using Docker Compose
1. Ensure you have a JWT public key at `./secrets/jwt_public_key.pem`.
2. Run:
   ```bash
   docker-compose up --build
   ```

### Using .NET CLI
Run from the project root directory (Environment variables are pre-configured in `launchSettings.json` for development):
```bash
dotnet run --project src/SS.APIGateway
```

## Configuration

Configuration is managed via `appsettings.json` and environment variables:
- `GATEWAY_HMAC_SECRET`: Secret key for HMAC signing.
- `Jwt:PublicKeyPath`: Path to the RSA public key file.
