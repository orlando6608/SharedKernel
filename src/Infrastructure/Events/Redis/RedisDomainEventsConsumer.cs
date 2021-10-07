﻿using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace SharedKernel.Infrastructure.Events.Redis
{
    /// <summary>
    /// 
    /// </summary>
    public class RedisDomainEventsConsumer : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serviceScopeFactory"></param>
        public RedisDomainEventsConsumer(IServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var initialScope = _serviceScopeFactory.CreateScope();

            var domainEventJsonDeserializer = initialScope.ServiceProvider.GetRequiredService<IDomainEventJsonDeserializer>();

            var domainEventMediator = initialScope.ServiceProvider.GetRequiredService<IDomainEventMediator>();

            var redisSubscriber = initialScope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>().GetSubscriber();

            await redisSubscriber.SubscribeAsync("*", async (_, value) =>
            {
                var @event = domainEventJsonDeserializer.Deserialize(value);

                var subscribers = DomainEventSubscriberInformationService.GetAllEventsSubscribers(@event);

                await Task.WhenAll(subscribers.Select(subscriber =>
                    domainEventMediator.ExecuteOn(value, @event, subscriber, stoppingToken)));
            });
        }
    }
}
