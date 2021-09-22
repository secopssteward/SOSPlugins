using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Redis.Fluent;
using SecOpsSteward.Plugins.Azure;
using SecOpsSteward.Plugins.Configurable;
using SecOpsSteward.Plugins.Discovery;
using SecOpsSteward.Plugins.WorkflowTemplates;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace SecOpsSteward.Plugins.RedisCache
{
    public class RedisCacheServiceConfiguration : AzureSharedConfiguration, IConfigurableObjectConfiguration
    {
        [Required]
        [IdentifiesTargetGrantScope]
        [DisplayName("Redis Cache Name")]
        public string CacheName { get; set; }

        internal string GetScope()
        {
            return "/subscriptions/" + SubscriptionId +
                   "/resourceGroups/" + ResourceGroup +
                   "/providers/Microsoft.Cache/redis/" + CacheName;
        }

        internal Task<IRedisCache> GetRedisCacheAsync(IAzure azure)
        {
            return azure.RedisCaches.GetByResourceGroupAsync(ResourceGroup, CacheName);
        }
    }

    [ElementDescription(
        "Azure Redis",
        "Manages Azure Redis Caches")]
    public class RedisCacheService : SOSManagedService<RedisCacheServiceConfiguration>
    {
        public RedisCacheService(AzureCurrentCredentialFactory platformFactory)
        {
            PlatformFactory = platformFactory;
            if (platformFactory == null) throw new Exception("Platform handle not found");
        }

        public RedisCacheService()
        {
        }

        public override ManagedServiceRole Role => ManagedServiceRole.Producer;

        public override List<WorkflowTemplateDefinition> Templates => new()
        {
            TEMPLATE_RESET_PASSWORD
        };

        private static WorkflowTemplateDefinition TEMPLATE_RESET_PASSWORD =>
            new WorkflowTemplateDefinition<RedisCacheService, RedisCacheServiceConfiguration>(
                    "Reset Redis Cache Key")
                .RunWorkflowStep<ResetCacheKey>();

        protected AzureCurrentCredentialFactory PlatformFactory { get; set; }

        public override async Task<List<DiscoveredServiceConfiguration>> Discover()
        {
            var azure = PlatformFactory.GetCredential(Configuration.SubscriptionId).GetAzure();

            IEnumerable<IRedisCache> redisCaches;
            if (string.IsNullOrEmpty(Configuration.ResourceGroup))
                redisCaches = await azure.RedisCaches.ListAsync();
            else
                redisCaches = await azure.RedisCaches.ListByResourceGroupAsync(Configuration.ResourceGroup);

            return (await Task.WhenAll(redisCaches.Select(async redisCache =>
            {
                await Task.Yield();
                return new DiscoveredServiceConfiguration
                {
                    ManagedServiceId = this.GenerateId(),
                    DescriptiveName = $"({this.GetDescriptiveName()}) {redisCache.ResourceGroupName} / {redisCache.Name}",
                    Configuration = new RedisCacheServiceConfiguration
                    {
                        TenantId = Configuration.TenantId,
                        SubscriptionId = Configuration.SubscriptionId,
                        ResourceGroup = redisCache.ResourceGroupName,
                        CacheName = redisCache.Name
                    },
                    Identifier = $"{redisCache.ResourceGroupName}/{redisCache.Name}",
                    LinksInAs = new List<string>
                    {
                        $"{redisCache.ResourceGroupName}/{redisCache.Name}",
                        redisCache.Name,

                        redisCache.HostName,
                        redisCache.StaticIP
                    }
                };
            }))).ToList();
        }
    }
}
