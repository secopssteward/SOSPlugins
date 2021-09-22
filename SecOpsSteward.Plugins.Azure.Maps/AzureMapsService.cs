using Microsoft.Azure.Management.CosmosDB.Fluent;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Maps;
using Microsoft.Azure.Management.Maps.Models;
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

namespace SecOpsSteward.Plugins.Azure.Maps
{
    public class AzureMapsServiceConfiguration : AzureSharedConfiguration, IConfigurableObjectConfiguration
    {
        [Required]
        [IdentifiesTargetGrantScope]
        [DisplayName("Maps Account")]
        public string MapsAccount { get; set; }

        internal string GetScope()
        {
            return "/subscriptions/" + SubscriptionId +
                   "/resourceGroups/" + ResourceGroup +
                   "/providers/Microsoft.Maps/accounts/" + MapsAccount;
        }
    }

    [ElementDescription(
        "Azure Maps",
        "Manages Azure Maps")]
    public class AzureMapsService : SOSManagedService<AzureMapsServiceConfiguration>
    {
        public MapsManagementClient GetManagementClient() =>
            new MapsManagementClient(PlatformFactory.GetCredential().AzureCredentials);

        public AzureMapsService(AzureCurrentCredentialFactory platformFactory)
        {
            PlatformFactory = platformFactory;
            if (platformFactory == null) throw new Exception("Platform handle not found");
        }

        public AzureMapsService()
        {
        }

        public override ManagedServiceRole Role => ManagedServiceRole.Producer;

        public override List<WorkflowTemplateDefinition> Templates => new()
        {
            TEMPLATE_RESET_KEY
        };

        private static WorkflowTemplateDefinition TEMPLATE_RESET_KEY =>
            new WorkflowTemplateDefinition<AzureMapsService, MapsRegenerateKeyConfiguration>("Reset Azure Maps Key")
                .RunWorkflowStep<MapsRegenerateKey>();

        protected AzureCurrentCredentialFactory PlatformFactory { get; set; }

        public override async Task<List<DiscoveredServiceConfiguration>> Discover()
        {
            var azure = PlatformFactory.GetCredential(Configuration.SubscriptionId).GetAzure();

            var allMaps = await GetManagementClient().Accounts.ListByResourceGroupAsync(Configuration.ResourceGroup);

            return (await Task.WhenAll(allMaps.Select(async maps =>
            {
                await Task.Yield();
                return new DiscoveredServiceConfiguration
                {
                    ManagedServiceId = this.GenerateId(),
                    DescriptiveName = $"({this.GetDescriptiveName()}) {maps.Name}",
                    Configuration = new AzureMapsServiceConfiguration
                    {
                        TenantId = Configuration.TenantId,
                        SubscriptionId = Configuration.SubscriptionId,
                        ResourceGroup = Configuration.ResourceGroup, // todo: how to get this from MapsAccount?
                        MapsAccount = maps.Name
                    },
                    Identifier = $"{maps.Id}",
                    LinksInAs = new List<string>
                    {
                        $"{Configuration.ResourceGroup}/{maps.Name}",
                        maps.Name
                    }
                };
            }))).ToList();
        }
    }
}
