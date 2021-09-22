using Microsoft.Azure.Management.CosmosDB.Fluent;
using Microsoft.Azure.Management.Eventhub.Fluent;
using Microsoft.Azure.Management.Fluent;
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

namespace SecOpsSteward.Plugins.Azure.EventHub
{
    public class EventHubNamespaceServiceConfiguration : AzureSharedConfiguration, IConfigurableObjectConfiguration
    {
        [Required]
        [IdentifiesTargetGrantScope]
        [DisplayName("Namespace")]
        public string Namespace { get; set; }

        internal string GetScope()
        {
            return "/subscriptions/" + SubscriptionId +
                   "/resourceGroups/" + ResourceGroup +
                   "/providers/Microsoft.EventHub/namespaces/" + Namespace;
        }

        internal Task<IEventHubNamespace> GetEventHubNamespaceAsync(IAzure azure)
        {
            return azure.EventHubNamespaces.GetByResourceGroupAsync(ResourceGroup, Namespace);
        }
    }

    [ElementDescription(
        "Azure Event Hub Namepace",
        "Manages Azure Event Hub Namespaces")]
    public class EventHubNamespaceService : SOSManagedService<EventHubNamespaceServiceConfiguration>
    {
        public EventHubNamespaceService(AzureCurrentCredentialFactory platformFactory)
        {
            PlatformFactory = platformFactory;
            if (platformFactory == null) throw new Exception("Platform handle not found");
        }

        public EventHubNamespaceService()
        {
        }

        public override ManagedServiceRole Role => ManagedServiceRole.Producer;

        public override List<WorkflowTemplateDefinition> Templates => new()
        {
            TEMPLATE_RESET_KEY
        };

        private static WorkflowTemplateDefinition TEMPLATE_RESET_KEY =>
            new WorkflowTemplateDefinition<EventHubNamespaceService, EventHubNamespaceRegenerateKeyConfiguration>("Reset Event Hub Namespace Key")
                .RunWorkflowStep<EventHubNamespaceRegenerateKey>();

        protected AzureCurrentCredentialFactory PlatformFactory { get; set; }

        public override async Task<List<DiscoveredServiceConfiguration>> Discover()
        {
            var azure = PlatformFactory.GetCredential(Configuration.SubscriptionId).GetAzure();

            var allEventHubNamespaces = await azure.EventHubNamespaces.ListByResourceGroupAsync(Configuration.ResourceGroup);

            return (await Task.WhenAll(allEventHubNamespaces.Select(async eventHubNamespace =>
            {
                await Task.Yield();
                return new DiscoveredServiceConfiguration
                {
                    ManagedServiceId = this.GenerateId(),
                    DescriptiveName = $"({this.GetDescriptiveName()}) {eventHubNamespace.ResourceGroupName} / {eventHubNamespace.Name}",
                    Configuration = new EventHubNamespaceServiceConfiguration
                    {
                        TenantId = Configuration.TenantId,
                        SubscriptionId = Configuration.SubscriptionId,
                        ResourceGroup = eventHubNamespace.ResourceGroupName,
                        Namespace = eventHubNamespace.Name
                    },
                    Identifier = $"{eventHubNamespace.ResourceGroupName}/{eventHubNamespace.Name}",
                    LinksInAs = new List<string>
                    {
                        $"{eventHubNamespace.ResourceGroupName}/{eventHubNamespace.Name}",
                        eventHubNamespace.Name,
                        eventHubNamespace.ServiceBusEndpoint
                    }
                };
            }))).ToList();
        }
    }
}
