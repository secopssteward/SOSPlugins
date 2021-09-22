using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Maps;
using Microsoft.Azure.Management.Maps.Models;
using SecOpsSteward.Plugins.Configurable;

namespace SecOpsSteward.Plugins.Azure.Maps
{
    public class MapsRegenerateKeyConfiguration : AzureMapsServiceConfiguration, IConfigurableObjectConfiguration
    {
        [DisplayName("Regenerate Primary Key?")]
        [Description("If true, the primary key will be regenerated. Otherwise the secondary key will be regenerated.")]
        public bool RegeneratePrimaryKey { get; set; }
    }

    [ElementDescription(
        "Regenerate Azure Maps Key",
        "Anthony Turner",
        "Regenerates a Key for an Azure Maps Account",
        "1.0.0")]
    [ManagedService(typeof(AzureMapsService))]
    [PossibleResultCodes(CommonResultCodes.Success, CommonResultCodes.Failure)]
    [GeneratedSharedOutputs(
        "Maps/{{$Configuration.ResourceGroup}}/{{$Configuration.MapsAccount}}/{{$Configuration.RegeneratePrimaryKey?key1:key2}}")]
    public class MapsRegenerateKey : SOSPlugin<MapsRegenerateKeyConfiguration>
    {
        public MapsRegenerateKey(AzureCurrentCredentialFactory platformFactory)
        {
            PlatformFactory = platformFactory;
            if (platformFactory == null) throw new Exception("Platform handle not found");
        }

        public MapsRegenerateKey()
        {
        }

        public MapsManagementClient GetManagementClient() =>
            new MapsManagementClient(PlatformFactory.GetCredential().AzureCredentials);

        protected AzureCurrentCredentialFactory PlatformFactory { get; set; }

        public override IEnumerable<PluginRbacRequirements> RbacRequirements => new[]
        {
            AzurePluginRbacRequirements.WithActions(
                "Read & Regenerate Azure Maps Account Keys",
                Configuration.GetScope(),
                "Microsoft.Maps/accounts/listKeys/action",
                "Microsoft.Maps/accounts/regenerateKey/action")
        };

        public override async Task<PluginOutputStructure> Execute(PluginOutputStructure previousOutput)
        {
            var mgmtClient = GetManagementClient();

            var key = new MapsKeySpecification(
                Configuration.RegeneratePrimaryKey ? "primary" : "secondary");

            var keys = await mgmtClient.Accounts.RegenerateKeysAsync(
                Configuration.ResourceGroup, Configuration.MapsAccount, key);

            return new PluginOutputStructure(CommonResultCodes.Success)
                .WithSecureOutput(
                    $"Maps/{Configuration.ResourceGroup}/{Configuration.MapsAccount}/{(Configuration.RegeneratePrimaryKey ? "key1" : "key2")}",
                    Configuration.RegeneratePrimaryKey ?
                        keys.PrimaryKey : keys.SecondaryKey);
        }
    }
}