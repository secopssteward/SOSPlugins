using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Azure.Management.CosmosDB.Fluent.Models;
using SecOpsSteward.Plugins.Configurable;

namespace SecOpsSteward.Plugins.Azure.CosmosDb
{
    public class DbRegenerateKeyConfiguration : CosmosDbServiceConfiguration, IConfigurableObjectConfiguration
    {
        [DisplayName("Regenerate Primary Key?")]
        [Description("If true, the primary key will be regenerated. Otherwise the secondary key will be regenerated.")]
        public bool RegeneratePrimaryKey { get; set; }

        [DisplayName("Regenerate Read-Only Key?")]
        [Description("If true, the read-only key will be regenerated. Otherwise the read-write key will be regenerated.")]
        public bool RegenerateReadOnlyKey { get; set; }
    }

    [ElementDescription(
        "Regenerate CosmosDB Database Key",
        "Anthony Turner",
        "Regenerates a Key for an Azure CosmosDB/DocumentDB Account",
        "1.0.0")]
    [ManagedService(typeof(CosmosDbService))]
    [PossibleResultCodes(CommonResultCodes.Success, CommonResultCodes.Failure)]
    [GeneratedSharedOutputs(
        "CosmosDb/{{$Configuration.ResourceGroup}}/{{$Configuration.DatabaseAccount}}/{{$Configuration.RegenerateReadOnlyKey?ro:rw}}/{{$Configuration.RegeneratePrimaryKey?key1:key2}}")]
    public class DbRegenerateKey : SOSPlugin<DbRegenerateKeyConfiguration>
    {
        public DbRegenerateKey(AzureCurrentCredentialFactory platformFactory)
        {
            PlatformFactory = platformFactory;
            if (platformFactory == null) throw new Exception("Platform handle not found");
        }

        public DbRegenerateKey()
        {
        }

        protected AzureCurrentCredentialFactory PlatformFactory { get; set; }

        public override IEnumerable<PluginRbacRequirements> RbacRequirements => new[]
        {
            Configuration.RegenerateReadOnlyKey ?
                AzurePluginRbacRequirements.WithActions(
                    "Read & Regenerate CosmosDB Read-Only Account Keys",
                    Configuration.GetScope(),
                    "Microsoft.DocumentDB/databaseAccounts/regenerateKey/action",
                    "Microsoft.DocumentDB/databaseAccounts/readonlykeys/action")
                :
                AzurePluginRbacRequirements.WithActions(
                    "Read & Regenerate CosmosDB Account Keys",
                    Configuration.GetScope(),
                    "Microsoft.DocumentDB/databaseAccounts/regenerateKey/action",
                    "Microsoft.DocumentDB/databaseAccounts/listKeys/action")
        };

        public override async Task<PluginOutputStructure> Execute(PluginOutputStructure previousOutput)
        {
            var azure = PlatformFactory.GetCredential(Configuration.SubscriptionId).GetAzure();

            var keyType = Configuration.RegeneratePrimaryKey ? "primary" : "secondary";
            if (Configuration.RegenerateReadOnlyKey)
                keyType += "ReadOnly";

            var response = await azure.CosmosDBAccounts.Inner.RegenerateKeyWithHttpMessagesAsync(Configuration.ResourceGroup, Configuration.DatabaseAccount,
                (Configuration.RegenerateReadOnlyKey ?
                    (Configuration.RegeneratePrimaryKey ? KeyKind.PrimaryReadonly : KeyKind.SecondaryReadonly) :
                    (Configuration.RegeneratePrimaryKey ? KeyKind.Primary : KeyKind.Secondary)));

            string key = string.Empty;
            if (Configuration.RegenerateReadOnlyKey)
            {
                var roKeys = await azure.CosmosDBAccounts.ListReadOnlyKeysAsync(
                    Configuration.ResourceGroup, Configuration.DatabaseAccount);
                key = Configuration.RegeneratePrimaryKey ? roKeys.PrimaryReadonlyMasterKey : roKeys.SecondaryReadonlyMasterKey;
            }
            else
            {
                var rwKeys = await azure.CosmosDBAccounts.ListKeysAsync(
                    Configuration.ResourceGroup, Configuration.DatabaseAccount);
                key = Configuration.RegeneratePrimaryKey ? rwKeys.PrimaryMasterKey : rwKeys.SecondaryMasterKey;
            }

            return new PluginOutputStructure(CommonResultCodes.Success)
                .WithSecureOutput(
                    $"CosmosDb/{Configuration.ResourceGroup}/{Configuration.DatabaseAccount}/{(Configuration.RegenerateReadOnlyKey ? "ro" : "rw")}/{(Configuration.RegeneratePrimaryKey ? "key1" : "key2")}",
                    key);
        }
    }
}