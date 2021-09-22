using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Sql.Fluent;
using SecOpsSteward.Plugins.Configurable;
using SecOpsSteward.Plugins.Discovery;
using SecOpsSteward.Plugins.WorkflowTemplates;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace SecOpsSteward.Plugins.Azure.Sql
{
    public class AzureSqlServiceConfiguration : AzureSharedConfiguration, IConfigurableObjectConfiguration
    {
        [Required]
        [IdentifiesTargetGrantScope]
        [DisplayName("Database Server")]
        public string DbServer { get; set; }

        internal string GetScope()
        {
            return "/subscriptions/" + SubscriptionId +
                   "/resourceGroups/" + ResourceGroup +
                   "/providers/Microsoft.Sql/servers/" + DbServer;
        }

        internal Task<ISqlServer> GetAzureSqlAsync(IAzure azure)
        {
            return azure.SqlServers.GetByResourceGroupAsync(ResourceGroup, DbServer);
        }
    }

    [ElementDescription(
        "Azure SQL",
        "Manages Azure SQL Servers & Databases")]
    public class AzureSqlService : SOSManagedService<AzureSqlServiceConfiguration>
    {
        public AzureSqlService(AzureCurrentCredentialFactory platformFactory)
        {
            PlatformFactory = platformFactory;
            if (platformFactory == null) throw new Exception("Platform handle not found");
        }

        public AzureSqlService()
        {
        }

        public override ManagedServiceRole Role => ManagedServiceRole.Producer;

        public override List<WorkflowTemplateDefinition> Templates => new()
        {
            TEMPLATE_RESET_PASSWORD
        };

        private static WorkflowTemplateDefinition TEMPLATE_RESET_PASSWORD =>
            new WorkflowTemplateDefinition<AzureSqlService, AzureSqlServiceConfiguration>(
                    "Reset SQL Azure Administrator Password")
                .RunWorkflowStep<ResetAdminPassword>();


        protected AzureCurrentCredentialFactory PlatformFactory { get; set; }

        public override async Task<List<DiscoveredServiceConfiguration>> Discover()
        {
            var azure = PlatformFactory.GetCredential(Configuration.SubscriptionId).GetAzure();

            IEnumerable<ISqlServer> allSql;
            if (string.IsNullOrEmpty(Configuration.ResourceGroup))
                allSql = await azure.SqlServers.ListAsync();
            else
                allSql = await azure.SqlServers.ListByResourceGroupAsync(Configuration.ResourceGroup);

            return (await Task.WhenAll(allSql.Select(async sql =>
            {
                await Task.Yield();
                return new DiscoveredServiceConfiguration
                {
                    ManagedServiceId = this.GenerateId(),
                    DescriptiveName = $"({this.GetDescriptiveName()}) {sql.ResourceGroupName} / {sql.Name}",
                    Configuration = new AzureSqlServiceConfiguration
                    {
                        TenantId = Configuration.TenantId,
                        SubscriptionId = Configuration.SubscriptionId,
                        ResourceGroup = sql.ResourceGroupName,
                        DbServer = sql.Name
                    },
                    Identifier = $"{sql.ResourceGroupName}/{sql.Name}",
                    LinksInAs = new List<string>
                    {
                        $"{sql.ResourceGroupName}/{sql.Name}",
                        sql.Name,

                        sql.FullyQualifiedDomainName
                    }
                };
            }))).ToList();
        }
    }
}
