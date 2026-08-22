# fpssoftware.chassis

Reusable, application-agnostic building blocks shared across FPS Software projects.

## Package

- Package id: `fpssoftware.chassis`
- Target framework: `netstandard2.1`
- Private feed: `https://gitea.fpssoftware.uk/api/packages/fps-software/nuget/index.json`

## Modules

- `AppResponse` / `AppResponse<T>` / `AppMessage` / `Paging` — universal result types
- `AppResponseExtensions.AddPrefix` — prefix message codes (e.g. `"Expense.Name"`)
- `ChassisJsonFormatter` — Serilog compact JSON formatter
- `FlagsEnumArrayConverter` — JSON.NET converter that serializes `[Flags]` enums as arrays of names
- Middleware (see below)
- JWT token creation / bearer authentication helpers (see below)

---

## Middleware

All middleware targets `netstandard2.1` via `Microsoft.AspNetCore.Http.Abstractions`, so any ASP.NET Core app (3.0+) can use it. Register them in `Program.cs` / `Startup.cs` with the extension methods below. Each middleware has a public options class so you can change its behaviour without forking the package.

### `CorrelationIdMiddleware`

**What it does:** ensures every request has a correlation id, propagates it as a header and into the Serilog diagnostic context, and pushes it as a Serilog log property for the duration of the request.

- Source precedence: user **claim** → inbound **header** → `HttpContext.TraceIdentifier` → new `Guid`.
- Inbound header is validated (must be a `Guid`, max 64 chars) before being reused.
- Writes `X-Correlation-Id` on both request and response, stores it in `HttpContext.Items`, and calls `IDiagnosticContext.Set(...)`.

**Usage:**

```csharp
// Program.cs
using FpsSoftware.Chassis;

var builder = WebApplication.CreateBuilder(args);

// Serilog must already be configured (Serilog.AspNetCore provides IDiagnosticContext)
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

var app = builder.Build();
app.UseCorrelationId();          // defaults: ClaimType "CorrelationId", HeaderName "X-Correlation-Id"
// or custom:
app.UseCorrelationId(new CorrelationIdOptions
{
    ClaimType = "TraceId",
    HeaderName = "X-Trace-Id",
});
```

### `ExceptionMiddleware`

**What it does:** catches unhandled exceptions, logs them, and returns a JSON error body instead of letting the exception propagate. Avoids leaking internals in production.

- Maps common exceptions to status codes: `ArgumentException` → 400, `UnauthorizedAccessException` → 401, `KeyNotFoundException` → 404, everything else → 500.
- In Development it includes `type` and `stackTrace`; in Production it returns only `statusCode`, a friendly `message`, `traceId`, and `timestamp`.
- If the response has already started, it logs and leaves it untouched.

**Usage:**

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseCustomExceptionHandler();
// or with custom friendly messages:
app.UseCustomExceptionHandler(new ExceptionMiddlewareOptions
{
    InvalidDataMessage = "The supplied data is not valid.",
    UnauthorizedMessage = "You are not allowed to do that.",
    NotFoundMessage = "We could not find what you asked for.",
    GenericErrorMessage = "Something went wrong. Please try again.",
});
```

> Register this **early** in the pipeline (before routing) so it can catch exceptions from everything downstream.

### `LocalizationMiddleware`

**What it does:** sets `CultureInfo.CurrentCulture` and `CurrentUICulture` from the request's `Accept-Language` header, so downstream code (resource lookups, formatting) uses the caller's locale. Falls back to a default culture when the header is missing or unparseable.

- Iterates `Accept-Language` values, strips quality parameters (e.g. `pt-BR;q=0.9` → `pt-BR`), and uses the first valid culture.
- Invalid cultures are skipped; if none match, the default culture is used.
- When the header is absent or has no valid value it sets the configured default culture (default `en-US`).

**Usage:**

```csharp
// Program.cs
var app = builder.Build();

app.UseLocationMiddleware();    // default culture "en-US"
// or:
app.UseLocationMiddleware(new LocalizationOptions { DefaultCulture = "pt-BR" });
```

> The culture change is visible to middleware and services that run *after* this one in the pipeline (it flows via `AsyncLocal` with the request), not to the code that registered the middleware.

### `SafeHeadersMiddleware`

**What it does:** appends baseline **security response headers** to every response to harden the app against common browser attacks.

Defaults:

| Header | Default value |
|--------|---------------|
| `Referrer-Policy` | `strict-origin-when-cross-origin` |
| `X-Content-Type-Options` | `nosniff` |
| `X-Frame-Options` | `DENY` |
| `Permissions-Policy` | `geolocation=(self), microphone=(), camera=()` |

**Usage:**

```csharp
// Program.cs
var app = builder.Build();

app.UseSafeHeaders();
// or fully customize:
app.UseSafeHeaders(new SafeHeadersOptions
{
    ReferrerPolicy = "no-referrer",
    XContentTypeOptions = "nosniff",
    XFrameOptions = "SAMEORIGIN",
    PermissionsPolicy = "geolocation=(self), microphone=()",
});
```

### `SecurityPolicyMiddleware`

**What it does:** serves the SPA `index.html` with a **Content-Security-Policy** nonce, and skips API routes and static assets.

- Passes through paths starting with `ApiPathPrefix` (default `/api`) and anything with a file extension.
- For other routes it generates a random nonce, replaces a placeholder in `index.html` and the CSP string with it, and writes the HTML with a `Content-Security-Policy` header.
- Bring your own `IFileProvider` (e.g. `app.Environment.WebRootFileProvider` or a `PhysicalFileProvider`) so the middleware remains generic.

**Usage:**

```csharp
// Program.cs
var app = builder.Build();

app.UseSecurityPolicy(new SecurityPolicyOptions
{
    ApiPathPrefix = "/api",
    NonceKey = "CSP-Nonce",
    NoncePlaceholder = "{{nonce}}",
    CspValue = "default-src 'self'; " +
               "script-src 'self' 'nonce-{{nonce}}'; " +
               "style-src 'self' 'nonce-{{nonce}}'; " +
               "connect-src 'self' https://api.example.com;",
    FileProvider = app.Environment.WebRootFileProvider,
});
```

Your `wwwroot/index.html` must contain the placeholder where the nonce goes, e.g.:

```html
<script nonce="{{nonce}}">/* inline script */</script>
```

The nonce is also stored in `HttpContext.Items[NonceKey]` so downstream code (e.g. tag helpers) can use it.

---

## JWT / authentication

`JwtTokenSettings` and `JwtTokenService` provide configurable JWT issuing and expiry-tolerant principal lookup. The consuming application supplies claims and user data — the package stays application-agnostic. Bearer/Identity setup stays in the consuming app's own composition layer (registering `Microsoft.AspNetCore.Authentication.JwtBearer` there, e.g. `AddJwtBearer`), so this library has no dependency on the version-fragile `JwtBearer` package.

### `JwtTokenService.CreateToken`

Creates an HS256-signed JWT from a `JwtTokenSettings` + your claims.

```csharp
using FpsSoftware.Chassis;

var settings = new JwtTokenSettings
{
    SecretKey = "<at-least-32-chars-secret>",
    Issuer = "https://api.example.com",
    Audience = "https://app.example.com",
    TokenExpireSeconds = 900,
    RefreshTokenExpireSeconds = 2592000,
};

var token = JwtTokenService.CreateToken(settings, new[]
{
    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
    new Claim(ClaimTypes.Role, "admin"),
    new Claim(ClaimTypes.Email, user.Email),
});
```

### `JwtTokenService.GetPrincipalFromExpiredToken`

Validates a (possibly expired) refresh token's signature/issuer/audience so you can read its claims and issue a new one. It deliberately ignores lifetime (`ValidateLifetime = false`).

```csharp
var principal = JwtTokenService.GetPrincipalFromExpiredToken(settings, refreshToken);
var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
```

> Throws `SecurityTokenException` if the token is not signed with HS256 or fails validation.

---

## Development

```bash
dotnet test
dotnet pack src/FpsSoftware.Chassis/FpsSoftware.Chassis.csproj -c Release
```

Publishing happens automatically on merge to `main` (see `.github/workflows/ci.yml`).
