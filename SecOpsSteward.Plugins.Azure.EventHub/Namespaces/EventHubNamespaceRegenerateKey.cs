using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Management.EventHub.Fluent.Models;
using SecOpsSteward.Plugins.Configurable;

namespace SecOpsSteward.Plugins.Azure.EventHub
{
    public class EventHubNamespaceRegenerateKeyConfiguration : EventHubNamespaceServiceConfiguration, IConfigurableObjectConfiguration
    {
        [DisplayName("Regenerate Primary Key?")]
        [Description("If true, the primary key will be regenerated. Otherwise the secondary key will be regenerated.")]
        public bool RegeneratePrimaryKey { get; set; }

        [DisplayName("Authorization Rule")]
        [Description("Authorization Rule Name")]
        public string AuthorizationRule { get; set; }
    }

    [ElementDescription(
        "Regenerate Event Hub Namespace Key",
        "Anthony Turner",
        "Regenerates a Key for an Azure Event Hub Namespace",
        "1.0.0")]
    [ManagedService(typeof(EventHubNamespaceService))]
    [PossibleResultCodes(CommonResultCodes.Success, CommonResultCodes.Failure)]
    [GeneratedSharedOutputs(
        "EventHub/{{$Configuration.ResourceGroup}}/{{$Configuration.Namespace}}/{{$Configuration.EventHub}}/{{$Configuration.AuthorizationRule}}/{{$Configuration.RegeneratePrimaryKey?key1:key2}}")]
    public class EventHubNamespaceRegenerateKey : SOSPlugin<EventHubNamespaceRegenerateKeyConfiguration>
    {
        public EventHubNamespaceRegenerateKey(AzureCurrentCredentialFactory platformFactory)
        {
            PlatformFactory = platformFactory;
            if (platformFactory == null) throw new Exception("Platform handle not found");
        }

        public EventHubNamespaceRegenerateKey()
        {
        }

        protected AzureCurrentCredentialFactory PlatformFactory { get; set; }

        public override IEnumerable<PluginRbacRequirements> RbacRequirements => new[]
        {
            AzurePluginRbacRequirements.WithActions(
                "Read & Regenerate Event Hub Namespace Keys",
                Configuration.GetScope(),
                "Microsoft.EventHub/namespaces/authorizationRules/regenerateKeys/action")
        };

        public override async Task<PluginOutputStructure> Execute(PluginOutputStructure previousOutput)
        {
            var azure = PlatformFactory.GetCredential(Configuration.SubscriptionId).GetAzure();

            var keys = await azure.EventHubNamespaces.AuthorizationRules.Inner.RegenerateKeysWithHttpMessagesAsync(
                Configuration.ResourceGroup, Configuration.Namespace, Configuration.AuthorizationRule,
                new RegenerateAccessKeyParameters(Configuration.RegeneratePrimaryKey ? KeyType.PrimaryKey : KeyType.SecondaryKey));

            var key = Configuration.RegeneratePrimaryKey ? keys.Body.PrimaryKey : keys.Body.SecondaryKey;

            return new PluginOutputStructure(CommonResultCodes.Success)
                .WithSecureOutput(
                    $"EventHub/{Configuration.ResourceGroup}/{Configuration.Namespace}/{Configuration.AuthorizationRule}/{(Configuration.RegeneratePrimaryKey ? "key1" : "key2")}",
                    key);
        }
    }
}