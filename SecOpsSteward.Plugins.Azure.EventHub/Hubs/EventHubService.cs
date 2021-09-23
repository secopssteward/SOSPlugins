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
    public class EventHubServiceConfiguration : AzureSharedConfiguration, IConfigurableObjectConfiguration
    {
        [Required]
        [IdentifiesTargetGrantScope]
        [DisplayName("Namespace")]
        public string Namespace { get; set; }
        
        [Required]
        [IdentifiesTargetGrantScope]
        [DisplayName("Event Hub Name")]
        public string EventHub { get; set; }

        internal string GetScope()
        {
            return "/subscriptions/" + SubscriptionId +
                   "/resourceGroups/" + ResourceGroup +
                   "/providers/Microsoft.EventHub/namespaces/" + Namespace +
                   "/eventhubs/" + EventHub;
        }

        internal Task<IEventHub> GetEventHubAsync(IAzure azure)
        {
            return azure.EventHubs.GetByNameAsync(ResourceGroup, Namespace, EventHub);
        }
    }

    [ElementDescription(
        "Azure Event Hub",
        "Manages Azure Event Hub")]
    [ServiceImage(ProviderImages.EVENT_HUB_SVG)]
    public class EventHubService : SOSManagedService<EventHubServiceConfiguration>
    {
        public EventHubService(AzureCurrentCredentialFactory platformFactory)
        {
            PlatformFactory = platformFactory;
            if (platformFactory == null) throw new Exception("Platform handle not found");
        }

        public EventHubService()
        {
        }

        public override ManagedServiceRole Role => ManagedServiceRole.Producer;

        public override List<WorkflowTemplateDefinition> Templates => new()
        {
            TEMPLATE_RESET_KEY
        };

        private static WorkflowTemplateDefinition TEMPLATE_RESET_KEY =>
            new WorkflowTemplateDefinition<EventHubService, EventHubRegenerateKeyConfiguration>("Reset Event Hub Key")
                .RunWorkflowStep<EventHubRegenerateKey>();

        protected AzureCurrentCredentialFactory PlatformFactory { get; set; }

        public override async Task<List<DiscoveredServiceConfiguration>> Discover()
        {
            var azure = PlatformFactory.GetCredential(Configuration.SubscriptionId).GetAzure();

            var allEventHubs = await azure.EventHubs.ListByNamespaceAsync(Configuration.ResourceGroup, Configuration.Namespace);

            return (await Task.WhenAll(allEventHubs.Select(async eventHub =>
            {
                await Task.Yield();
                return new DiscoveredServiceConfiguration
                {
                    ManagedServiceId = this.GenerateId(),
                    DescriptiveName = $"({this.GetDescriptiveName()}) {eventHub.NamespaceResourceGroupName} / {eventHub.NamespaceName} / {eventHub.Name}",
                    Configuration = new EventHubServiceConfiguration
                    {
                        TenantId = Configuration.TenantId,
                        SubscriptionId = Configuration.SubscriptionId,
                        ResourceGroup = eventHub.NamespaceResourceGroupName,
                        Namespace = eventHub.NamespaceName,
                        EventHub = eventHub.Name
                    },
                    Identifier = $"{eventHub.NamespaceResourceGroupName}/{eventHub.NamespaceName}/{eventHub.Name}",
                    LinksInAs = new List<string>
                    {
                        $"{eventHub.NamespaceResourceGroupName}/{eventHub.NamespaceName}/{eventHub.Name}",
                        eventHub.Name
                    }
                };
            }))).ToList();
        }
    }
}
