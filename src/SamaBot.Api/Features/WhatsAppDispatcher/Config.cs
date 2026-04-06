using Refit;

namespace SamaBot.Api.Features.WhatsAppDispatcher;

public static class Config
{
    public static IServiceCollection AddWhatsAppDispatcherFeature(this IServiceCollection services, IConfiguration config)
    {
        var baseUrl = config["WhatsApp:BaseUrl"] ?? "https://graph.facebook.com/v19.0";

        services.AddRefitClient<IWhatsAppClient>()
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(baseUrl);
            });

        return services;
    }
}