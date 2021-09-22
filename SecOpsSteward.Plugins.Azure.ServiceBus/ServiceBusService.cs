using Microsoft.Azure.Management.CosmosDB.Fluent;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ServiceBus.Fluent;
using SecOpsSteward.Plugins.Configurable;
using SecOpsSteward.Plugins.Discovery;
using SecOpsSteward.Plugins.WorkflowTemplates;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecOpsSteward.Plugins.Azure.ServiceBus
{
    public class ServiceBusServiceConfiguration : AzureSharedConfiguration, IConfigurableObjectConfiguration
    {
        [Required]
        [IdentifiesTargetGrantScope]
        [DisplayName("Service Bus Namespace")]
        public string Namespace { get; set; }

        internal string GetScope()
        {
            return "/subscriptions/" + SubscriptionId +
                   "/resourceGroups/" + ResourceGroup +
                   "/providers/Microsoft.ServiceBus/namespaces/" + Namespace;
        }

        internal Task<IServiceBusNamespace> GetServiceBusAsync(IAzure azure)
        {
            return azure.ServiceBusNamespaces.GetByResourceGroupAsync(ResourceGroup, Namespace);
        }
    }

    [ElementDescription(
        "Azure CosmosDB",
        "Manages Azure CosmosDB/DocumentDB")]
    public class ServiceBusService : SOSManagedService<ServiceBusServiceConfiguration>
    {
        public ServiceBusService(AzureCurrentCredentialFactory platformFactory)
        {
            PlatformFactory = platformFactory;
            if (platformFactory == null) throw new Exception("Platform handle not found");
        }

        public ServiceBusService()
        {
        }

        public override ManagedServiceRole Role => ManagedServiceRole.Producer;

        public override List<WorkflowTemplateDefinition> Templates => new()
        {
            TEMPLATE_RESET_KEY
        };

        private static WorkflowTemplateDefinition TEMPLATE_RESET_KEY =>
            new WorkflowTemplateDefinition<ServiceBusService, ServiceBusNamespaceRegenerateKeyConfiguration>("Regenerate Service Bus Namespace Key")
                .RunWorkflowStep<ServiceBusNamespaceRegenerateKey>();

        protected AzureCurrentCredentialFactory PlatformFactory { get; set; }

        public override async Task<List<DiscoveredServiceConfiguration>> Discover()
        {
            var azure = PlatformFactory.GetCredential(Configuration.SubscriptionId).GetAzure();

            IEnumerable<IServiceBusNamespace> sbNamespaces;
            if (string.IsNullOrEmpty(Configuration.ResourceGroup))
                sbNamespaces = await azure.ServiceBusNamespaces.ListAsync();
            else
                sbNamespaces = await azure.ServiceBusNamespaces.ListByResourceGroupAsync(Configuration.ResourceGroup);

            return (await Task.WhenAll(sbNamespaces.Select(async serviceBus =>
            {
                await Task.Yield();
                return new DiscoveredServiceConfiguration
                {
                    ManagedServiceId = this.GenerateId(),
                    DescriptiveName = $"({this.GetDescriptiveName()}) {serviceBus.ResourceGroupName} / {serviceBus.Name}",
                    Configuration = new ServiceBusServiceConfiguration
                    {
                        TenantId = Configuration.TenantId,
                        SubscriptionId = Configuration.SubscriptionId,
                        ResourceGroup = serviceBus.ResourceGroupName,
                        Namespace = serviceBus.Name
                    },
                    Identifier = $"{serviceBus.ResourceGroupName}/{serviceBus.Name}",
                    LinksInAs = new List<string>
                    {
                        $"{serviceBus.ResourceGroupName}/{serviceBus.Name}",
                        serviceBus.Name,
                        serviceBus.Fqdn,
                        serviceBus.DnsLabel
                    }
                };
            }))).ToList();
        }
    }
}
