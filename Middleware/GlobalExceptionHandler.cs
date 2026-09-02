using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Middleware;

/// Last-resort safety net for genuinely unanticipated exceptions — registered via
/// AddExceptionHandler&lt;GlobalExceptionHandler&gt;() + app.UseExceptionHandler() in Program.cs so it
/// wraps the whole pipeline (including CORS/auth middleware failures), unlike an MVC IExceptionFilter.
/// Intentionally separate from Services/, which is reserved for business logic returning
/// ServiceResult&lt;T&gt; — this class writes directly to HttpContext.Response instead.
/// Does not apply to ChatController's SSE streaming endpoints: those self-handle exceptions via their
/// own try/catch + an "error" SSE event, and once Response.HasStarted is true the built-in
/// exception-handler middleware detects this and rethrows without invoking TryHandleAsync at all.
/// AddExceptionHandler&lt;T&gt;() registers IExceptionHandler as a Singleton, so ICurrentUserContext
/// (Scoped) can't be a constructor dependency here — it's resolved per-request from
/// httpContext.RequestServices inside TryHandleAsync instead.
public class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment env) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var currentUser = httpContext.RequestServices.GetRequiredService<ICurrentUserContext>();

        logger.LogError(exception,
            "Unhandled exception. Path={Path} Method={Method} UserId={UserId} SocietyId={SocietyId} Role={Role}",
            httpContext.Request.Path, httpContext.Request.Method,
            currentUser.UserId, currentUser.SocietyId, currentUser.RoleName);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/json";

        // Same { message: "..." } envelope every controller already returns for errors (e.g.
        // ResidentsController's `new { message = result.Error }`) so src/api/client.js needs no changes.
        // Never a stack trace, in any environment; the exception's own message only in Development,
        // where it's the fastest way to see what broke without switching to the console log.
        var message = env.IsDevelopment()
            ? $"An unexpected error occurred: {exception.Message}"
            : "An unexpected error occurred. Please try again, and contact support if the problem persists.";

        await httpContext.Response.WriteAsJsonAsync(new { message }, ct);

        return true;
    }
}
