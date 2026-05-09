# SamStore API Gateway Matrix

This document provides a comprehensive overview of all routes proxied by the `SS.APIGateway`, including security requirements, rate-limiting policies, and downstream service mappings.

## Endpoint Matrix

| Route Path | Methods | Auth | Rate Limit | Target Cluster | Description |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Auth Service** | | | | | |
| `/api/auth/login` | `POST` | Anonymous | `brute-force` | `auth-cluster` | User authentication. |
| `/api/auth/register` | `POST` | Anonymous | `anti-abuse` | `auth-cluster` | New user registration. |
| `/api/auth/forgot-password` | `POST` | Anonymous | `anti-abuse` | `auth-cluster` | Trigger recovery email. |
| `/api/auth/reset-password` | `POST` | Anonymous | `anti-abuse` | `auth-cluster` | Complete password reset. |
| `/api/auth/{**catch-all}` | `ANY` | Anonymous | `global` | `auth-cluster` | Generic auth endpoints. |
| `/api/mfa/verify` | `POST` | Anonymous | `brute-force` | `auth-cluster` | Verify MFA/TOTP codes. |
| `/api/mfa/{**catch-all}` | `ANY` | **Protected** | `global` | `auth-cluster` | MFA management. |
| `/api/user/{**catch-all}` | `ANY` | **Protected** | `global` | `auth-cluster` | Profile & Identity mgmt. |
| `/api/roles/{**catch-all}` | `ANY` | **Protected** | `global` | `auth-cluster` | RBAC management. |
| `/api/menus/{**catch-all}` | `ANY` | **Protected** | `global` | `auth-cluster` | Navigation management. |
| **Catalog Service** | | | | | |
| `/api/catalog/{**catch-all}` | `GET` | Anonymous | `global` | `catalog-cluster`| Public product browsing. |
| `/api/catalog/{**catch-all}` | `WRITE`*| **Protected** | `global` | `catalog-cluster`| Inventory management. |
| **Order Service** | | | | | |
| `/api/orders/{**catch-all}` | `ANY` | **Protected** | `global` | `orders-cluster` | Order processing. |

*\*WRITE = POST, PUT, PATCH, DELETE*

---

## Security Policies

### 1. Authorization
- **Anonymous**: No JWT required. Usually limited by stricter rate limits.
- **Protected**: Valid RS256 JWT required. The gateway validates the token and injects identity claims (`X-User-Id`, `X-User-Email`, `X-User-Roles`) before forwarding to downstream services.

### 2. Rate Limiting Tiers
All rate limits are applied per client IP (respecting `X-Forwarded-For`).
- **Global**: 200 requests / minute.
- **Anti-Abuse**: 10 requests / minute. (Applied to registration and recovery).
- **Brute-Force**: 5 requests / minute. (Applied to login and MFA verification).

### 3. Identity Propagation
Downstream services should trust the following headers injected by the gateway:
- `X-User-Id`: The unique identifier of the authenticated user.
- `X-User-Email`: The user's email address.
- `X-User-Roles`: Comma-separated list of roles.
- `X-Internal-Signature`: HMAC-SHA256 signature to verify the request originated from the Gateway.

---

## Usage Examples (curl)

### Authenticate (Login)
```bash
curl -X POST http://localhost:8080/api/auth/login \
     -H "Content-Type: application/json" \
     -d '{"email": "user@example.com", "password": "password123"}'
```

### Access Protected Profile
```bash
# Replace <TOKEN> with the JWT received from login
curl -X GET http://localhost:8080/api/user/profile \
     -H "Authorization: Bearer <TOKEN>"
```

### Trigger Rate Limit (429)
```bash
# Run this 6 times within a minute
curl -I -X POST http://localhost:8080/api/auth/login
# Result: HTTP/1.1 429 Too Many Requests
```
