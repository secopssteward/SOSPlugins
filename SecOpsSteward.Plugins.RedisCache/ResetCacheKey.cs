using Microsoft.Azure.Management.Redis.Fluent.Models;
using SecOpsSteward.Plugins.Azure;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace SecOpsSteward.Plugins.RedisCache
{
    public class RedisRegenerateKeyConfiguration : RedisCacheServiceConfiguration
    {
        [DisplayName("Regenerate Primary Key?")]
        [Description("If true, the primary key will be regenerated. Otherwise the secondary key will be regenerated.")]
        public bool RegeneratePrimaryKey { get; set; }
    }
        
    [ElementDescription(
        "Reset Redis Cache Key",
        "Anthony Turner",
        "Regenerates a Redis Cache key",
        "1.0.0")]
    [ManagedService(typeof(RedisCacheService))]
    [PossibleResultCodes(CommonResultCodes.Success, CommonResultCodes.Failure)]
    [GeneratedSharedOutputs(
        "RedisCache/{{$Configuration.ResourceGroup}}/{{$Configuration.CacheName}}/{{$Configuration.RegeneratePrimaryKey?key1:key2}}")]
    public class ResetCacheKey : SOSPlugin<RedisRegenerateKeyConfiguration>
    {
        public ResetCacheKey(AzureCurrentCredentialFactory platformFactory)
        {
            PlatformFactory = platformFactory;
            if (platformFactory == null) throw new Exception("Platform handle not found");
        }

        public ResetCacheKey()
        {
        }

        protected AzureCurrentCredentialFactory PlatformFactory { get; set; }

        public override IEnumerable<PluginRbacRequirements> RbacRequirements => new[]
        {
            AzurePluginRbacRequirements.WithActions(
                "Reset Redis Cache Key",
                Configuration.GetScope(),
                // TODO: CONFIRM !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                "Microsoft.Cache/redis/regenerateKey/action",
                "Microsoft.Cache/redis/listKeys/action")
        };

        public override async Task<PluginOutputStructure> Execute(PluginOutputStructure previousOutput)
        {
            var azure = PlatformFactory.GetCredential(Configuration.SubscriptionId).GetAzure();

            var redisCache = await Configuration.GetRedisCacheAsync(azure);
            var keys = redisCache.RegenerateKey(Configuration.RegeneratePrimaryKey ? RedisKeyType.Primary : RedisKeyType.Secondary);
            var key = Configuration.RegeneratePrimaryKey ? keys.PrimaryKey : keys.SecondaryKey;

            return new PluginOutputStructure(CommonResultCodes.Success)
                .WithSecureOutput(
                    $"RedisCache/{Configuration.ResourceGroup}/{Configuration.CacheName}/{(Configuration.RegeneratePrimaryKey ? "key1" : "key2")}", key);
        }
    }
}