# ASP.NET Core Custom API Rate Limiter

A high-performance, custom rate-limiting solution built as ASP.NET Core middleware. It features unified storage abstractions (Memory & Redis support), policy-based throttling (e.g. Standard vs. Premium endpoints), IP-address/Client-Header extraction, and automatic bypass of unmatched routes (404s) to preserve user quota.

---

## Features

- **Unified Storage Interface**: Implementations for in-memory cache (`IMemoryCache`) and distributed cache (`IDistributedCache` / Redis).
- **Endpoint Throttling (Policies)**: Decorate specific routes/endpoints with different request thresholds (e.g. limit 3 requests/10s vs 10 requests/10s).
- **Bypass 404 Pages**: Intelligent route check (`context.GetEndpoint()`) to prevent unmapped pages or typos from consuming rate limit budgets.
- **Detailed Telemetry**: Returns rate-limiting headers (`X-RateLimit-Limit`, `X-RateLimit-Remaining`) and standard HTTP 429 payload containing a `Retry-After` header.
- **Diagnostic Logging**: Structured logger entries for all evaluation, acceptance, and block actions.

---

## Project Structure

- **[Middleware/](file:///c:/Backup%20from%20Previous/DotNetPractice/RateLimitor/Middleware)**: Contains `RateLimitingMiddleware.cs` for request throttling execution.
- **[Services/](file:///c:/Backup%20from%20Previous/DotNetPractice/RateLimitor/Services)**: Contains the `IRateLimitStore` contract along with its in-memory and distributed implementation layers.
- **[Models/](file:///c:/Backup%20from%20Previous/DotNetPractice/RateLimitor/Models)**: Custom Strong-typed `RateLimitOptions.cs` configurations.
- **[Attributes/](file:///c:/Backup%20from%20Previous/DotNetPractice/RateLimitor/Attributes)**: Custom route decorator attributes.

---

## Configuration (`appsettings.json`)

Configure policies and storage engines globally:

```json
"RateLimiting": {
  "StorageType": "Memory", // Or "Redis"
  "Policies": {
    "Default": {
      "PermitLimit": 3,
      "WindowSeconds": 10
    },
    "Premium": {
      "PermitLimit": 10,
      "WindowSeconds": 10
    }
  }
}
```

---

## Getting Started

### Prerequisites
- .NET 10 SDK

### Run the App
Restore dependencies and start the API:
```bash
dotnet run --urls="http://localhost:5088"
```

---

## Testing Scenarios

Use your favorite HTTP client, curl, or the built-in [RateLimitor.http](file:///c:/Backup%20from%20Previous/DotNetPractice/RateLimitor/RateLimitor.http) scratchpad to test these scenarios:

### 1. Default Route (Limits: 3 requests / 10s)
Request the endpoint `/api/default`:
```bash
curl -i -H "X-Client-Id: client1" http://localhost:5088/api/default
```
- First 3 requests: returns `200 OK` along with `X-RateLimit-Limit: 3` and `X-RateLimit-Remaining`.
- 4th request: returns `429 Too Many Requests` with a `Retry-After: 10` header.

### 2. Premium Route (Limits: 10 requests / 10s)
Request the endpoint `/api/premium`:
```bash
curl -i -H "X-Client-Id: premium_user" http://localhost:5088/api/premium
```
- First 10 requests: returns `200 OK`.
- 11th request: returns `429 Too Many Requests`.

### 3. IP Throttling (Header Fallback)
If no `X-Client-Id` header is passed, the middleware throttles by remote IP address:
```bash
curl -i http://localhost:5088/api/default
```
