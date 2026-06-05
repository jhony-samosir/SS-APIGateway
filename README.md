# SS-APIGateway

## Overview

`SS-APIGateway` adalah API Gateway tingkat enterprise untuk platform e-commerce SamStore. Dibangun di atas **YARP (Yet Another Reverse Proxy)** dan **ASP.NET Core 10**, gateway ini berfungsi sebagai **single entry point** dari semua request client (SS-App) ke seluruh microservice backend.

Gateway mengimplementasikan model **Zero Trust Security**: setiap request divalidasi JWT-nya di perimeter, identity-nya disuntikkan ke header internal, dan semua request yang diteruskan ke downstream service ditandatangani dengan **HMAC-SHA256** agar microservice dapat memverifikasi bahwa request berasal dari gateway yang sah.

---

## Tech Stack

| Kategori       | Teknologi                                     |
| -------------- | --------------------------------------------- |
| Runtime        | .NET 10.0 (C#)                                |
| Reverse Proxy  | YARP (Yet Another Reverse Proxy)              |
| Resilience     | Polly (Retry, Timeout, Circuit Breaker)       |
| Logging        | Serilog + CompactJsonFormatter                |
| Telemetry      | OpenTelemetry (Traces + Metrics)              |
| Containerisasi | Docker, Docker Compose                        |

---

## Struktur Proyek

```text
SS-APIGateway/
├── src/
│   └── SS.APIGateway/
│       ├── Common/                    # Konstanta claim JWT (UserId, PublicId, Role, Permissions)
│       ├── Configuration/             # POCO options: JwtOptions, InternalSignatureOptions, RateLimitOptions
│       ├── Extensions/
│       │   ├── AuthenticationExtensions.cs   # Registrasi JWT RS256 bearer auth
│       │   ├── CorsSecurityExtensions.cs     # CORS policy
│       │   ├── ObservabilityExtensions.cs    # OpenTelemetry setup
│       │   ├── RateLimitExtensions.cs        # Fixed-window rate limiters
│       │   └── ResiliencyExtensions.cs       # Polly pipeline (Timeout→Retry→CircuitBreaker)
│       ├── Middleware/
│       │   ├── CorrelationIdMiddleware.cs    # Propagasi X-Correlation-ID ke semua request
│       │   └── SecurityHeadersMiddleware.cs  # Injeksi OWASP security headers pada response
│       ├── Transforms/
│       │   ├── IdentityTransformProvider.cs         # Strip spoofable headers + inject X-User-* dari JWT claims
│       │   └── InternalOriginSignatureTransform.cs  # Menandatangani request ke downstream dengan HMAC-SHA256
│       ├── Program.cs                 # Entry point & pipeline konfigurasi
│       ├── appsettings.json           # Konfigurasi dasar
│       ├── appsettings.Development.json  # Konfigurasi dev (routing YARP lengkap)
│       └── appsettings.Production.json   # Konfigurasi production
├── tests/
│   └── SS.APIGateway.Tests/           # Unit test untuk transforms & middleware
├── secrets/                           # JWT public key PEM (tidak di-commit ke Git)
├── docker-compose.yml                 # Orkestrasi seluruh stack SamStore
├── Dockerfile                         # Multi-stage build
└── SS.APIGateway.slnx
```

---

## Middleware Pipeline

Request masuk diproses dalam urutan berikut:

```
[Client Request]
       │
       ▼
ForwardedHeaders       ← Resolve X-Forwarded-For / X-Forwarded-Proto
       │
       ▼
SecurityHeadersMiddleware  ← Tambah OWASP headers (HSTS, X-Frame-Options, CSP, dll)
       │
       ▼
CorrelationIdMiddleware    ← Generate/propagasi X-Correlation-ID
       │
       ▼
SerilogRequestLogging      ← Structured HTTP access log
       │
       ▼
CORS                       ← GatewayPolicy (whitelist origins)
       │
       ▼
RateLimiter                ← Global / BruteForce / AntiAbuse policy
       │
       ▼
Authentication             ← JWT RS256 Bearer token validation
       │
       ▼
Authorization              ← Policy: anonymous / RequireAuthenticatedUser
       │
       ▼
YARP Reverse Proxy
  ├── IdentityTransformProvider        ← Strip spoofable headers, inject X-User-* dari JWT
  ├── InternalOriginSignatureTransform ← Sign request dengan HMAC-SHA256
  └── ResiliencyExtensions             ← Polly: Timeout → Retry → Circuit Breaker
       │
       ▼
[Downstream Microservice]
```

---

## YARP Routing Table

Semua route dikonfigurasi di `appsettings.Development.json` / `appsettings.Production.json` di bawah key `ReverseProxy`.

| Route ID                   | Path Pattern                        | Methods              | Cluster          | Auth Policy               | Rate Limit     |
| -------------------------- | ----------------------------------- | -------------------- | ---------------- | ------------------------- | -------------- |
| `auth-login-route`         | `/api/auth/login`                   | POST                 | auth-cluster     | anonymous                 | brute-force    |
| `auth-register-route`      | `/api/auth/register`                | POST                 | auth-cluster     | anonymous                 | anti-abuse     |
| `auth-forgot-password-route` | `/api/auth/forgot-password`       | POST                 | auth-cluster     | anonymous                 | anti-abuse     |
| `auth-reset-password-route` | `/api/auth/reset-password`         | POST                 | auth-cluster     | anonymous                 | anti-abuse     |
| `auth-verify-email-route`  | `/api/auth/verify-email`            | GET                  | auth-cluster     | anonymous                 | global         |
| `auth-refresh-route`       | `/api/auth/refresh`                 | POST                 | auth-cluster     | anonymous                 | anti-abuse     |
| `auth-logout-route`        | `/api/auth/logout`                  | POST                 | auth-cluster     | anonymous                 | global         |
| `mfa-verify-route`         | `/api/mfa/verify`                   | ALL                  | auth-cluster     | anonymous                 | brute-force    |
| `mfa-protected-route`      | `/api/mfa/{**catch-all}`            | ALL                  | auth-cluster     | RequireAuthenticatedUser  | global         |
| `user-protected-route`     | `/api/user/{**catch-all}`           | ALL                  | auth-cluster     | RequireAuthenticatedUser  | global         |
| `roles-protected-route`    | `/api/roles/{**catch-all}`          | ALL                  | auth-cluster     | RequireAuthenticatedUser  | global         |
| `menus-protected-route`    | `/api/menus/{**catch-all}`          | ALL                  | auth-cluster     | RequireAuthenticatedUser  | global         |
| `security-protected-route` | `/api/security/{**catch-all}`       | ALL                  | auth-cluster     | RequireAuthenticatedUser  | global         |
| `catalog-read-route`       | `/api/catalog/{**catch-all}`        | GET                  | catalog-cluster  | anonymous (JWT optional)  | global         |
| `catalog-write-route`      | `/api/catalog/{**catch-all}`        | POST/PUT/PATCH/DELETE | catalog-cluster | RequireAuthenticatedUser  | global         |
| `orders-route`             | `/api/orders/{**catch-all}`         | ALL                  | orders-cluster   | RequireAuthenticatedUser  | global         |
| `cart-route`               | `/api/cart/{**catch-all}`           | ALL                  | cart-cluster     | RequireAuthenticatedUser  | global         |
| `profile-route`            | `/api/profiles/{**catch-all}`       | ALL                  | profile-cluster  | RequireAuthenticatedUser  | global         |

### Cluster Destinations (Internal Docker Network)

| Cluster          | Service          | Address                        |
| ---------------- | ---------------- | ------------------------------ |
| auth-cluster     | auth-service     | `http://auth-service:8080`     |
| catalog-cluster  | catalog-service  | `http://catalog-service:8081`  |
| orders-cluster   | orders-service   | `http://orders-service:5003`   |
| cart-cluster     | cart-service     | `http://cart-service:8083`     |
| profile-cluster  | profile-service  | `http://profile-service:8080`  |

---

## Resilience Policies (Polly)

Setiap cluster mendapat pipeline Polly yang diisolasi:

| Layer           | Konfigurasi Default                                                    |
| --------------- | ---------------------------------------------------------------------- |
| **Timeout**     | 30 detik per request                                                   |
| **Retry**       | Maks 3x, exponential backoff + jitter. Hanya untuk metode idempotent (GET, PUT, DELETE, HEAD, OPTIONS) dan error transient (5xx / HttpRequestException) |
| **Circuit Breaker** | Break jika failure ratio ≥ 50% dalam window 30 detik, minimum 10 request. Break duration 30 detik. Diisolasi per cluster. |

---

## Rate Limiting Policies

| Policy        | Permit Limit | Window   | Queue Limit |
| ------------- | ------------ | -------- | ----------- |
| `global`      | 200 req      | 1 menit  | 50          |
| `brute-force` | 5 req        | 1 menit  | 0           |
| `anti-abuse`  | 10 req       | 1 menit  | 0           |

---

## Security Headers (OWASP)

`SecurityHeadersMiddleware` menambahkan header berikut pada **setiap response**:

| Header                      | Nilai                                          |
| --------------------------- | ---------------------------------------------- |
| `X-Content-Type-Options`    | `nosniff`                                      |
| `X-Frame-Options`           | `DENY`                                         |
| `Referrer-Policy`           | `strict-origin-when-cross-origin`              |
| `Permissions-Policy`        | `geolocation=(), camera=(), microphone=()`     |
| `Strict-Transport-Security` | `max-age=31536000; includeSubDomains; preload` |
| `Content-Security-Policy`   | `default-src 'none'; frame-ancestors 'none'` (hanya non-proxied) |
| `X-Powered-By`              | *Dihapus*                                      |
| `X-AspNet-Version`          | *Dihapus*                                      |

---

## Zero Trust: Identity Injection

`IdentityTransformProvider` dijalankan pada setiap route dengan `RequiresJwt = "true"`:

1. **Strip** header spoofable dari client: `X-User-Id`, `X-User-Roles`, `X-User-Permissions`, `X-User-PublicId`, `X-Internal-Signature`.
2. Propagasi `accessToken` cookie ke header `Authorization: Bearer <token>`.
3. **Inject** header internal bersih dari claims JWT yang telah divalidasi:

| Header                | Source Claim     |
| --------------------- | ---------------- |
| `X-User-Id`           | `userId`         |
| `X-User-PublicId`     | `publicId`       |
| `X-User-Roles`        | `role`           |
| `X-User-Permissions`  | `permissions`    |

---

## Environment Variables

| Variable                        | Deskripsi                                                      | Wajib |
| ------------------------------- | -------------------------------------------------------------- | ----- |
| `GATEWAY_HMAC_SECRET`           | Secret key HMAC-SHA256 untuk menandatangani request downstream | ✅    |
| `Jwt__PublicKeyPath`            | Path ke file PEM public key RSA untuk verifikasi JWT          | ✅    |
| `OpenTelemetry__Endpoint`       | Endpoint OTel Collector (contoh: `http://otel-collector:4317`) | ✅    |
| `OpenTelemetry__ServiceName`    | Nama service untuk telemetri (default: `ss-api-gateway`)       | ✅    |
| `ASPNETCORE_ENVIRONMENT`        | Environment runtime (`Development` / `Production`)             | ✅    |

---

## Instalasi & Menjalankan

### Prasyarat

- .NET 10.0 SDK
- Docker & Docker Compose
- RSA key pair (public/private) untuk JWT

### Setup

```bash
git clone <repository>
cd SamStore/SS-APIGateway
cp .env.example .env
# Edit GATEWAY_HMAC_SECRET di .env
```

Pastikan file `secrets/jwt_public_key.pem` tersedia.

### Menjalankan Lokal (.NET CLI)

```bash
dotnet run --project src/SS.APIGateway
```

Gateway akan berjalan di `http://localhost:8080`.

### Menjalankan dengan Docker Compose (Full Stack)

```bash
docker-compose up --build
```

Ini akan menjalankan seluruh stack: Gateway, semua microservice, Meilisearch, OTel Collector, Tempo, Loki, Fluent Bit, dan Grafana.

### Build

```bash
dotnet build src/SS.APIGateway/SS.APIGateway.csproj
```

### Testing

```bash
dotnet test
```

---

## Endpoints Gateway Sendiri

| Method | Path      | Deskripsi                          |
| ------ | --------- | ---------------------------------- |
| GET    | `/health` | Health check (akses anonim)        |

---

## Observability

- **Logging**: Serilog dengan `CompactJsonFormatter` ke stdout. Di Docker, di-forward ke Fluent Bit (port `24224`) dengan tag `samstore.<container_name>`.
- **Tracing**: OpenTelemetry traces dikirim ke OTel Collector via OTLP gRPC (`http://otel-collector:4317`).
- **Correlation ID**: Setiap request mendapat/mempertahankan `X-Correlation-ID` yang dipropagasikan ke downstream.

---

## CORS Allowed Origins

```
http://localhost:3000
http://127.0.0.1:3000
https://app.samstore.com
https://admin.samstore.com
```

---

## Deployment

- **Docker**: Multi-stage `Dockerfile` (build image .NET SDK → runtime image .NET ASP.NET).
- **Docker Compose**: Definisi orchestrasi lengkap di `docker-compose.yml`, termasuk seluruh stack infrastruktur.

---

## Known Issues

- Health check aktif per cluster (`Active.Enabled: false`) belum diaktifkan; saat ini hanya passive health check.

## Future Improvements

- Tambah dynamic routing reload dari database atau service discovery.
- Integrasikan distributed rate limiting dengan Redis backend.
- Aktifkan active health check per cluster.
