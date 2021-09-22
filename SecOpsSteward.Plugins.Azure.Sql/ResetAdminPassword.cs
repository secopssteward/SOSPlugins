using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SecOpsSteward.Plugins.Azure.Sql
{
    [ElementDescription(
        "Reset SQL Azure Administrator Password",
        "Anthony Turner",
        "Regenerates the Administrator password for an Azure SQL Server",
        "1.0.0")]
    [ManagedService(typeof(AzureSqlService))]
    [PossibleResultCodes(CommonResultCodes.Success, CommonResultCodes.Failure)]
    [GeneratedSharedOutputs(
        "AzureSql/{{$Configuration.ResourceGroup}}/{{$Configuration.StorageAccount}}")]
    public class ResetAdminPassword : SOSPlugin<AzureSqlServiceConfiguration>
    {
        public ResetAdminPassword(AzureCurrentCredentialFactory platformFactory)
        {
            PlatformFactory = platformFactory;
            if (platformFactory == null) throw new Exception("Platform handle not found");
        }

        public ResetAdminPassword()
        {
        }

        protected AzureCurrentCredentialFactory PlatformFactory { get; set; }

        public override IEnumerable<PluginRbacRequirements> RbacRequirements => new[]
        {
            AzurePluginRbacRequirements.WithActions(
                "Reset Azure SQL Admin Password",
                Configuration.GetScope(),
                // TODO: CONFIRM !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                "Microsoft.Sql/servers/administrators/write")
        };

        public override async Task<PluginOutputStructure> Execute(PluginOutputStructure previousOutput)
        {
            var azure = PlatformFactory.GetCredential(Configuration.SubscriptionId).GetAzure();

            var newPassword = PluginSharedHelpers.RandomString(32);
            var sqlServer = await Configuration.GetAzureSqlAsync(azure);
            await sqlServer.Update().WithAdministratorPassword(newPassword).ApplyAsync();

            return new PluginOutputStructure(CommonResultCodes.Success)
                .WithSecureOutput(
                    $"AzureSql/{Configuration.ResourceGroup}/{Configuration.DbServer}", newPassword);
        }
    }
}