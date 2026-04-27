using KJ.Infrastructure.Messaging.Consumers;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace KJ.Infrastructure.Messaging;

public static class MassTransitSetup
{
    public static IServiceCollection AddKjMassTransit(this IServiceCollection services)
    {
        services.AddSingleton<TagValuePublishingBridge>();

        services.AddMassTransit(x =>
        {
            x.AddConsumer<TagValueChangedConsumer>();
            x.AddConsumer<AlarmTriggeredConsumer>();
            x.AddConsumer<DeviceStateChangedConsumer>();
            x.AddConsumer<RecipeAppliedConsumer>();

            x.UsingInMemory((context, cfg) =>
            {
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}

