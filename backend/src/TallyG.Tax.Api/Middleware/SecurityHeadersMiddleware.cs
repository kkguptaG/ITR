namespace TallyG.Tax.Api.Middleware;

/// <summary>
/// Adds defence-in-depth security response headers to every API response.
///
/// This is a JSON API serving dynamic, frequently sensitive data (PII, PAN, income,
/// tax computations), so the headline control is <c>Cache-Control: no-store</c> — no
/// browser, proxy or CDN should ever retain a tax response (a DPDP/data-minimisation
/// concern). The rest are standard hardening: block MIME-sniffing, deny framing, and
/// strip the referrer. HSTS is emitted only in Production over HTTPS so it never
/// interferes with plain-HTTP local dev. The "Server" banner is dropped here as a
/// belt-and-braces complement to Kestrel's <c>AddServerHeader = false</c>.
///
/// Headers are written via <see cref="HttpResponse.OnStarting(Func{Task})"/> so they
/// land on EVERY response, including ones produced by the exception middleware and by
/// short-circuiting handlers. Registered first in the pipeline.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly bool _isProduction;

    public SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment env)
    {
        _next = next;
        _isProduction = env.IsProduction();
    }

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var (ctx, isProduction) = ((HttpContext, bool))state;
            var headers = ctx.Response.Headers;

            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["X-Permitted-Cross-Domain-Policies"] = "none";

            // Never cache API responses — they are dynamic and usually carry PII/tax data.
            // Respect a handler that deliberately set its own caching policy.
            if (!headers.ContainsKey("Cache-Control"))
            {
                headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
                headers["Pragma"] = "no-cache";
            }

            // Force HTTPS for two years (incl. subdomains) — Production only, and only once
            // the request actually arrived over TLS, so we never pin HSTS during local dev.
            if (isProduction && ctx.Request.IsHttps)
            {
                headers["Strict-Transport-Security"] = "max-age=63072000; includeSubDomains";
            }

            // Drop the framework/server banner (info-leak hygiene).
            headers.Remove("Server");

            return Task.CompletedTask;
        }, (context, _isProduction));

        return _next(context);
    }
}
