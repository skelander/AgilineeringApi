# Security

## Reporting a vulnerability

Open an issue or contact the maintainer directly.

---

## Conscious security tradeoffs

The following items were identified during a security audit and consciously accepted. Each entry documents what was found, why it was not changed, and what mitigates the risk.

### Default admin credentials (`admin` / `admin`)

**Finding:** The database seed creates an admin user with `admin:admin`.
**Decision:** Accepted for development and demo use. Must be changed before any public deployment.
**Mitigation:** The seed only runs when the `Users` table is empty (`if (!await db.Users.AnyAsync())`). In production, change the password via direct DB access or add a password-change endpoint.

---

### JWT stored in browser `localStorage`

**Finding (A07):** The access token is stored in `localStorage`, making it accessible to JavaScript and therefore vulnerable to XSS theft.
**Decision:** Accepted. The alternative — `httpOnly` cookies — would require a same-origin setup or a dedicated cookie-issuing proxy, which is out of scope for this SPA + separate API architecture.
**Mitigation:**
- The API enforces CORS to the known frontend origin only.
- The `Content-Security-Policy` header on the API limits what scripts can execute on API responses.
- Tokens expire after 8 hours.
- Any XSS that could steal tokens would also be able to make authenticated requests directly, so token theft is not the primary XSS risk.

---

### Client-side role guard

**Finding (A01):** The Vue router guard checks `localStorage.role === 'admin'` to protect the `/admin` route. An attacker can set this value in DevTools to view the admin UI.
**Decision:** Accepted. The guard is a UX convenience, not a security control. Every admin API endpoint is protected server-side with `[Authorize(Roles = "admin")]`. An attacker who bypasses the frontend guard gains access to HTML forms whose API calls will be rejected with 401/403.
**Mitigation:** All sensitive operations are enforced at the API layer.

---

### Image upload: magic-byte verification implemented

**Finding (A04):** Uploaded images must match their declared extension at the byte level.
**Implementation:** `ImagesService.UploadAsync` reads the file header and checks magic bytes for all supported formats (jpg, png, gif, webp). Files whose contents do not match the declared extension are rejected with 400.
**Remaining mitigations:**
- Upload is restricted to authenticated admins only — not a public endpoint.
- Uploaded files are stored with random GUID filenames, stripping any executable name.
- The `X-Content-Type-Options: nosniff` header prevents browsers from sniffing content type.
- Files are served as static content, not executed by the server.

---

### No preview link expiry

**Finding (A04):** Preview tokens are valid indefinitely once created. A leaked link remains usable forever.
**Decision:** Accepted for now. Adding expiry requires a DB schema change, a background cleanup job, and frontend changes.
**Mitigation:**
- Preview links are password-protected (minimum 6 characters, BCrypt work factor 12).
- The access endpoint is rate-limited (10 req/min per IP).
- The access endpoint (`POST /posts/preview/{token}/access`) returns the same 401 for both missing tokens and wrong credentials to prevent enumeration.
- The existence-check endpoint (`GET /posts/preview/{token}`) does reveal whether a token is valid (returns 200 vs 404). This is an intentional UX tradeoff to show a clear error page before the user attempts to log in. The token space is 128 bits (GUID N format), making enumeration infeasible in practice. The endpoint is also rate-limited.
- Admins can revoke access by deleting the post (cascades to all its previews).
