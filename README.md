# Telegram Init Data Validator (C#)

A tiny helper to verify **Telegram Mini App** `initData` on your backend.  
Implements the same algorithm as the official Go example: exclude `hash`, sort pairs, build `data_check_string`, and verify HMAC-SHA256 using the `WebAppData`-seeded secret.

---

## What this method is

```csharp
public static bool Validate(string initData, string botToken, TimeSpan expIn, out string? error)
```

**Purpose:**  
Validates `initData` received from a Telegram WebApp/mini app to confirm that:
1. The payload wasn’t tampered with (HMAC signature matches).
2. (Optionally) The payload is fresh (not older than `expIn`).

**Parameters:**
- `initData` — Raw querystring-like data you got from the client (form‑urlencoded; `+` treated as space).
- `botToken` — Your bot’s token; used to derive the secret per Telegram’s spec.
- `expIn` — Allowed lifetime of the payload (e.g., `TimeSpan.FromHours(24)`). Use `TimeSpan.Zero` to skip expiry checks.
- `error` — Out parameter with a short explanation on failure (`"hash sign is missing"`, `"init data is expired"`, etc.).

**Returns:**
- `true` if signature (and freshness) checks pass;
- `false` otherwise (reason in `error`).

**How it works (brief):**
- Parses `initData` as form-url-encoded key/value pairs.
- Removes `hash` from the set.
- Sorts the remaining `k=v` pairs lexicographically and joins with `\n` → `data_check_string`.
- Derives secret: `HMAC_SHA256(key="WebAppData", msg=botToken)`.
- Computes `calc = hex(HMAC_SHA256(key=secret, msg=data_check_string))`.
- Compares `calc` with provided `hash` (case-insensitive).
- If `expIn > 0`, checks `auth_date + expIn` against `UtcNow`.

---

## Minimal usage example

```csharp
// Program.cs (minimal)
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

var app = WebApplication.Create(args);

string botToken = "<YOUR_BOT_TOKEN>";
var expIn = TimeSpan.FromHours(24);

app.MapPost("/validate", async (HttpRequest req) =>
{
    var raw = await new StreamReader(req.Body).ReadToEndAsync();
    if (string.IsNullOrWhiteSpace(raw))
        return Results.BadRequest(new { ok = false, error = "init data is empty" });

    var ok = TelegramInitDataValidator.Validate(raw, botToken, expIn, out var err);
    return Results.Json(new { ok, error = err });
});

app.Run();
```

> ⚠️ In production, accept `initData` via HTTPS only and **do not log** the raw value.

---

## Important: Testing & Verification Required

This code is intentionally small and close to the Go reference logic, but **you must test it in your environment**:

- **Happy path:** valid signature, within `expIn`.
- **Missing/invalid `hash`.**
- **Expired `auth_date`** (e.g., `expIn = TimeSpan.FromMinutes(1)`).
- **Encoding quirks:** spaces vs `+`, percent-encoding of nested JSON fields (e.g., `user={...}`).
- **Order invariance:** ensure sorting and newline join exactly match the spec.
- **Time drift:** validate behavior when server time is skewed.

Add unit tests for:
- `TryParseQueryPublic` behavior on tricky inputs,
- `HexEqual` case-insensitivity,
- Known-good vectors from your live traffic.

---

## References & Examples

The implementation mirrors the official examples and docs. See:

**Go library this was modeled from:**
```
https://github.com/Telegram-Mini-Apps/init-data-golang
```

**Official docs (Node & Go examples included):**
```
https://docs.telegram-mini-apps.com/platform/authorizing-user
```

Use these to cross-check behavior, signatures, and edge-cases.

---

## Security notes & best practices

- **Never trust client-side checks**; always verify `initData` on your server.
- **Keep your bot token secret**; if it leaks, attackers can forge signatures.
- **Set a sensible `expIn`** (e.g., 24h) to limit replay risk.
- **Don’t log raw `initData`** in prod. If you must, mask sensitive fields.

---

## FAQ

**Q: Can I skip expiry checks?**  
A: Yes—pass `TimeSpan.Zero` as `expIn`. Not recommended for production.

**Q: What if I receive JSON instead of a query string?**  
A: Extract the raw `initData` string from the JSON payload as-is and feed it to `Validate`.

**Q: Why is `+` treated as a space?**  
A: Because Telegram sends form-url-encoded data; `+` is the canonical encoding for a space in this format.
