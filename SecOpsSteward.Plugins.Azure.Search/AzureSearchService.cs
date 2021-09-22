using Microsoft.Azure.Management.CosmosDB.Fluent;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Search.Fluent;
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

namespace SecOpsSteward.Plugins.Azure.Search
{
    public class AzureSearchServiceConfiguration : AzureSharedConfiguration, IConfigurableObjectConfiguration
    {
        [Required]
        [IdentifiesTargetGrantScope]
        [DisplayName("Database Account")]
        public string SearchService { get; set; }

        internal string GetScope()
        {
            return "/subscriptions/" + SubscriptionId +
                   "/resourceGroups/" + ResourceGroup +
                   "/providers/Microsoft.DocumentDB/databaseAccounts/" + SearchService;
        }

        internal Task<ICosmosDBAccount> GetCosmosDbAsync(IAzure azure)
        {
            return azure.CosmosDBAccounts.GetByResourceGroupAsync(ResourceGroup, SearchService);
        }
    }

    [ElementDescription(
        "Azure Search",
        "Manages Azure Search Account")]
    public class AzureSearchService : SOSManagedService<AzureSearchServiceConfiguration>
    {
        public AzureSearchService(AzureCurrentCredentialFactory platformFactory)
        {
            PlatformFactory = platformFactory;
            if (platformFactory == null) throw new Exception("Platform handle not found");
        }

        public AzureSearchService()
        {
        }

        public override ManagedServiceRole Role => ManagedServiceRole.Producer;

        public override List<WorkflowTemplateDefinition> Templates => new()
        {
            TEMPLATE_RESET_KEY
        };

        private static WorkflowTemplateDefinition TEMPLATE_RESET_KEY =>
            new WorkflowTemplateDefinition<AzureSearchService, SearchRegenerateKeyConfiguration>("Reset Azure Search Key")
                .RunWorkflowStep<SearchRegenerateKey>();

        protected AzureCurrentCredentialFactory PlatformFactory { get; set; }

        public override async Task<List<DiscoveredServiceConfiguration>> Discover()
        {
            var azure = PlatformFactory.GetCredential(Configuration.SubscriptionId).GetAzure();

            IEnumerable<ISearchService> allSearch;
            if (string.IsNullOrEmpty(Configuration.ResourceGroup))
                allSearch = await azure.SearchServices.ListAsync();
            else
                allSearch = await azure.SearchServices.ListByResourceGroupAsync(Configuration.ResourceGroup);

            return (await Task.WhenAll(allSearch.Select(async search =>
            {
                await Task.Yield();
                return new DiscoveredServiceConfiguration
                {
                    ManagedServiceId = this.GenerateId(),
                    DescriptiveName = $"({this.GetDescriptiveName()}) {search.ResourceGroupName} / {search.Name}",
                    Configuration = new AzureSearchServiceConfiguration
                    {
                        TenantId = Configuration.TenantId,
                        SubscriptionId = Configuration.SubscriptionId,
                        ResourceGroup = search.ResourceGroupName,
                        SearchService = search.Name
                    },
                    Identifier = $"{search.ResourceGroupName}/{search.Name}",
                    LinksInAs = new List<string>
                    {
                        $"{search.ResourceGroupName}/{search.Name}",
                        search.Name
                    }
                };
            }))).ToList();
        }
    }
}
