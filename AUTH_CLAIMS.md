# Frontend Authentication & Identity Claims

This document provides guidance for the `SS-App` (Frontend) on how to handle authentication and interpret user identity claims provided by the `SS-AuthService` through the `SS-APIGateway`.

## 1. Authentication Flow

The Gateway validates JWT tokens using **RS256**. It is stateless and does not make database calls for every request.

- **Login**: Frontend sends credentials to `/api/auth/login`. Gateway proxies this to `AuthService`.
- **Token Storage**: Store the received JWT in an HTTP-only secure cookie or local storage (as per architecture).
- **Subsequent Requests**: Include the token in the `Authorization: Bearer <TOKEN>` header.

## 2. JWT Claims

The Gateway is configured to use **raw claim names**. When decoding the JWT on the frontend, expect the following payload structure:

| Claim | Description | Mapping to Downstream Header |
| :--- | :--- | :--- |
| `sub` | Canonical User ID (UUID). | `X-User-Id` |
| `public_id` | User's public identifier for URLs. | `X-User-PublicId` |
| `role` | User's primary role (e.g., `Admin`, `Customer`). | `X-User-Roles` |
| `permissions` | Comma-separated list of granular permissions. | `X-User-Permissions` |

### Example Decoded Payload (Redacted)
```json
{
  "iss": "ss-auth-service",
  "aud": "ss-app",
  "sub": "550e8400-e29b-41d4-a716-446655440000",
  "public_id": "USR-928374",
  "role": "Admin",
  "permissions": "users.read,users.write,catalog.manage",
  "exp": 1715366400,
  "iat": 1715362800
}
```

## 3. Frontend Routing & Logic

### Admin vs Guest
- **Guest**: If no token is present or the Gateway returns `401 Unauthorized`.
- **Customer**: `role == "Customer"`. Can access personal orders and profile.
- **Admin**: `role == "Admin"`. Can access the management dashboard.

### Handling Permissions
For fine-grained UI control (e.g., hiding a "Delete" button), check the `permissions` claim:
```javascript
const hasPermission = (claim, perm) => claim?.split(',').includes(perm);
```

## 4. Gateway Integration

Downstream services (Catalog, Order, etc.) do NOT see the JWT. They receive pre-validated identity headers:
- `X-User-Id`
- `X-User-PublicId`
- `X-User-Roles`
- `X-User-Permissions`

The Gateway automatically strips any client-supplied headers with these names to prevent **Header Spoofing**.
