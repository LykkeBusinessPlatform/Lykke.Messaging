using System;
using Lykke.Messaging.RabbitMq.Retry;
using Microsoft.Extensions.DependencyInjection;

namespace Lykke.Messaging.RabbitMq
{
    public static class RabbitMqMessagingServiceCollectionExtensions
    {
        /// <summary>
        /// Adds RabbitMQ services to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddRabbitMqMessaging(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }
            
            services.AddOptions();
            services.AddSingleton<IRetryPolicyFactory, ConnectionRetryPolicyFactory>();
            services.AddSingleton<IRetryPolicyProvider, ConnectionRetryPolicyProvider>();
            return services;
        }
    }
}