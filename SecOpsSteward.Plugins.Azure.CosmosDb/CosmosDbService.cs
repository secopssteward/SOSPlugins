using Microsoft.Azure.Management.CosmosDB.Fluent;
using Microsoft.Azure.Management.Fluent;
using SecOpsSteward.Plugins.Configurable;
using SecOpsSteward.Plugins.Discovery;
using SecOpsSteward.Plugins.WorkflowTemplates;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace SecOpsSteward.Plugins.Azure.CosmosDb
{
    public class CosmosDbServiceConfiguration : AzureSharedConfiguration, IConfigurableObjectConfiguration
    {
        [Required]
        [IdentifiesTargetGrantScope]
        [DisplayName("Database Account")]
        public string DatabaseAccount { get; set; }

        internal string GetScope()
        {
            return "/subscriptions/" + SubscriptionId +
                   "/resourceGroups/" + ResourceGroup +
                   "/providers/Microsoft.DocumentDB/databaseAccounts/" + DatabaseAccount;
        }

        internal Task<ICosmosDBAccount> GetCosmosDbAsync(IAzure azure)
        {
            return azure.CosmosDBAccounts.GetByResourceGroupAsync(ResourceGroup, DatabaseAccount);
        }
    }

    [ElementDescription(
        "Azure CosmosDB",
        "Manages Azure CosmosDB/DocumentDB")]
    public class CosmosDbService : SOSManagedService<CosmosDbServiceConfiguration>
    {
        public CosmosDbService(AzureCurrentCredentialFactory platformFactory)
        {
            PlatformFactory = platformFactory;
            if (platformFactory == null) throw new Exception("Platform handle not found");
        }

        public CosmosDbService()
        {
        }

        public override ManagedServiceRole Role => ManagedServiceRole.Producer;

        public override List<WorkflowTemplateDefinition> Templates => new()
        {
            TEMPLATE_RESET_KEY
        };

        private static WorkflowTemplateDefinition TEMPLATE_RESET_KEY =>
            new WorkflowTemplateDefinition<CosmosDbService, DbRegenerateKeyConfiguration>("Reset CosmosDB Key")
                .RunWorkflowStep<DbRegenerateKey>();

        protected AzureCurrentCredentialFactory PlatformFactory { get; set; }

        public override async Task<List<DiscoveredServiceConfiguration>> Discover()
        {
            var azure = PlatformFactory.GetCredential(Configuration.SubscriptionId).GetAzure();

            IEnumerable<ICosmosDBAccount> allCosmos;
            if (string.IsNullOrEmpty(Configuration.ResourceGroup))
                allCosmos = await azure.CosmosDBAccounts.ListAsync();
            else
                allCosmos = await azure.CosmosDBAccounts.ListByResourceGroupAsync(Configuration.ResourceGroup);

            return (await Task.WhenAll(allCosmos.Select(async cosmos =>
            {
                await Task.Yield();
                return new DiscoveredServiceConfiguration
                {
                    ManagedServiceId = this.GenerateId(),
                    DescriptiveName = $"({this.GetDescriptiveName()}) {cosmos.ResourceGroupName} / {cosmos.Name}",
                    Configuration = new CosmosDbServiceConfiguration
                    {
                        TenantId = Configuration.TenantId,
                        SubscriptionId = Configuration.SubscriptionId,
                        ResourceGroup = cosmos.ResourceGroupName,
                        DatabaseAccount = cosmos.Name
                    },
                    Identifier = $"{cosmos.ResourceGroupName}/{cosmos.Name}",
                    LinksInAs = new List<string>
                    {
                        $"{cosmos.ResourceGroupName}/{cosmos.Name}",
                        cosmos.Name,
                        cosmos.DocumentEndpoint
                    }
                };
            }))).ToList();
        }
    }
}
