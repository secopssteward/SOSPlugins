using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Search.Fluent.Models;
using SecOpsSteward.Plugins.Configurable;

namespace SecOpsSteward.Plugins.Azure.Search
{
    public class SearchRegenerateKeyConfiguration : AzureSearchServiceConfiguration, IConfigurableObjectConfiguration
    {
        [DisplayName("Regenerate Primary Key?")]
        [Description("If true, the primary key will be regenerated. Otherwise the secondary key will be regenerated.")]
        public bool RegeneratePrimaryKey { get; set; }

        [DisplayName("Regenerate Read-Only Key?")]
        [Description("If true, the read-only key will be regenerated. Otherwise the read-write key will be regenerated.")]
        public bool RegenerateReadOnlyKey { get; set; }
    }

    [ElementDescription(
        "Regenerate Azure Search Key",
        "Anthony Turner",
        "Regenerates a Key for an Azure Search Service",
        "1.0.0")]
    [ManagedService(typeof(AzureSearchService))]
    [PossibleResultCodes(CommonResultCodes.Success, CommonResultCodes.Failure)]
    [GeneratedSharedOutputs(
        "Search/{{$Configuration.ResourceGroup}}/{{$Configuration.DatabaseAccount}}/{{$Configuration.RegenerateReadOnlyKey?ro:rw}}/{{$Configuration.RegeneratePrimaryKey?key1:key2}}")]
    public class SearchRegenerateKey : SOSPlugin<SearchRegenerateKeyConfiguration>
    {
        public SearchRegenerateKey(AzureCurrentCredentialFactory platformFactory)
        {
            PlatformFactory = platformFactory;
            if (platformFactory == null) throw new Exception("Platform handle not found");
        }

        public SearchRegenerateKey()
        {
        }

        protected AzureCurrentCredentialFactory PlatformFactory { get; set; }

        public override IEnumerable<PluginRbacRequirements> RbacRequirements => new[]
        {
            AzurePluginRbacRequirements.WithActions(
                "Read & Regenerate Search Account Keys",
                Configuration.GetScope(),
                "Microsoft.Search/searchServices/listAdminKeys/action",
                "Microsoft.Search/searchServices/regenerateAdminKey/action")
        };

        public override async Task<PluginOutputStructure> Execute(PluginOutputStructure previousOutput)
        {
            var azure = PlatformFactory.GetCredential(Configuration.SubscriptionId).GetAzure();

            var keyKind = Configuration.RegeneratePrimaryKey ? AdminKeyKind.Primary : AdminKeyKind.Secondary;
            var newKeys = await azure.SearchServices.RegenerateAdminKeysAsync(
                Configuration.ResourceGroup,
                Configuration.SearchService,
                keyKind);

            return new PluginOutputStructure(CommonResultCodes.Success)
                .WithSecureOutput(
                    $"Search/{Configuration.ResourceGroup}/{Configuration.SearchService}/{(Configuration.RegeneratePrimaryKey ? "key1" : "key2")}",
                    Configuration.RegeneratePrimaryKey ? newKeys.PrimaryKey : newKeys.SecondaryKey);
        }
    }
}