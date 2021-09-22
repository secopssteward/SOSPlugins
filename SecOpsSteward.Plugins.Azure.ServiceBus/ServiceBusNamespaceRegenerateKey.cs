using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Azure.Management.ServiceBus.Fluent.Models;
using SecOpsSteward.Plugins.Configurable;

namespace SecOpsSteward.Plugins.Azure.ServiceBus
{
    public class ServiceBusNamespaceRegenerateKeyConfiguration : ServiceBusServiceConfiguration, IConfigurableObjectConfiguration
    {
        [DisplayName("Regenerate Primary Key?")]
        [Description("If true, the primary key will be regenerated. Otherwise the secondary key will be regenerated.")]
        public bool RegeneratePrimaryKey { get; set; }

        [DisplayName("Authorization Rule")]
        [Description("Name of the Authorization Rule to regenerate")]
        public string AuthorizationRule { get; set; }

        internal string GetRuleScope()
        {
            return "/subscriptions/" + SubscriptionId +
                   "/resourceGroups/" + ResourceGroup +
                   "/providers/Microsoft.ServiceBus/namespaces/" + Namespace + 
                   "/AuthorizationRules/" + AuthorizationRule;
        }
    }

    [ElementDescription(
        "Regenerate Service Bus Namespace Key",
        "Anthony Turner",
        "Regenerates a Key for an Azure Service Bus Namespace",
        "1.0.0")]
    [ManagedService(typeof(ServiceBusService))]
    [PossibleResultCodes(CommonResultCodes.Success, CommonResultCodes.Failure)]
    [GeneratedSharedOutputs(
        "ServiceBus/{{$Configuration.ResourceGroup}}/{{$Configuration.Namespace}}/{{$Configuration.AuthorizationRule}}/{{$Configuration.RegeneratePrimaryKey?key1:key2}}")]
    public class ServiceBusNamespaceRegenerateKey : SOSPlugin<ServiceBusNamespaceRegenerateKeyConfiguration>
    {
        public ServiceBusNamespaceRegenerateKey(AzureCurrentCredentialFactory platformFactory)
        {
            PlatformFactory = platformFactory;
            if (platformFactory == null) throw new Exception("Platform handle not found");
        }

        public ServiceBusNamespaceRegenerateKey()
        {
        }

        protected AzureCurrentCredentialFactory PlatformFactory { get; set; }

        public override IEnumerable<PluginRbacRequirements> RbacRequirements => new[]
        {
            // ---------------------------------------------------------------------------------
            AzurePluginRbacRequirements.WithActions(
                "Read/Write Authorization Rules and Regenerate Service Bus Namespace Keys",
                Configuration.GetRuleScope(),
                "Microsoft.ServiceBus/namespaces/AuthorizationRules/regenerateKeys/action")
        };

        public override async Task<PluginOutputStructure> Execute(PluginOutputStructure previousOutput)
        {
            var azure = PlatformFactory.GetCredential(Configuration.SubscriptionId).GetAzure();

            var keys = await azure.ServiceBusNamespaces.Inner.RegenerateKeysWithHttpMessagesAsync(
                Configuration.ResourceGroup, Configuration.Namespace, Configuration.AuthorizationRule,
                Configuration.RegeneratePrimaryKey ? Policykey.PrimaryKey : Policykey.SecondaryKey);

            var key = Configuration.RegeneratePrimaryKey ? keys.Body.PrimaryKey : keys.Body.SecondaryKey;

            return new PluginOutputStructure(CommonResultCodes.Success)
                .WithSecureOutput(
                    $"ServiceBus/{Configuration.ResourceGroup}/{Configuration.Namespace}/{(Configuration.AuthorizationRule)}/{(Configuration.RegeneratePrimaryKey ? "key1" : "key2")}",
                    key);
        }
    }
}