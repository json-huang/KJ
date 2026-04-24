using KJ.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace KJ.Infrastructure.DependencyInjection;

public static class MessagingExtensions
{
    /// <summary>注册 MassTransit 进程内总线（与设计文档 §5.3 一致，先 InMemory）。</summary>
    public static IServiceCollection AddKjMessaging(this IServiceCollection services) =>
        MassTransitSetup.AddKjMassTransit(services);
}
