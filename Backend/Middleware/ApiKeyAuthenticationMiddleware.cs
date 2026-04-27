using CentralAuthNotificationPlatform.Models;
using CentralAuthNotificationPlatform.Services;

namespace CentralAuthNotificationPlatform.Middleware;

public sealed class ApiKeyAuthenticationMiddleware(RequestDelegate next)
{
    public const string ExternalAppItemKey = "ExternalApp";
    private const string HeaderName = "X-API-Key";

    public async Task InvokeAsync(HttpContext context, IExternalAppService externalAppService)
    {
        if (!RequiresApiKey(context))
        {
            await next(context);
            return;
        }

        var apiKey = context.Request.Headers[HeaderName].FirstOrDefault();
        var externalApp = await externalAppService.ValidateApiKeyAsync(apiKey, context.RequestAborted);

        if (externalApp is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    code = "INVALID_API_KEY",
                    message = "A valid X-API-Key header is required."
                }
            }, context.RequestAborted);
            return;
        }

        context.Items[ExternalAppItemKey] = externalApp;
        await next(context);
    }

    public static ExternalApp? GetExternalApp(HttpContext context)
    {
        return context.Items.TryGetValue(ExternalAppItemKey, out var value)
            ? value as ExternalApp
            : null;
    }

    private static bool RequiresApiKey(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api/integrations", StringComparison.OrdinalIgnoreCase) ||
            context.Request.Path.StartsWithSegments("/api/integration", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return HttpMethods.IsPost(context.Request.Method) &&
            context.Request.Path.Equals("/api/auth/forgot-password", StringComparison.OrdinalIgnoreCase);
    }
}
