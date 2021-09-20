using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SecOpsSteward.Plugins.Configurable;

namespace SecOpsSteward.Plugins.WorkflowTemplates
{
    /// <summary>
    ///     Defines a templated workflow which can be inserted into a user-created workflow
    /// </summary>
    public class WorkflowTemplateDefinition
    {
        /// <summary>
        ///     Fixed workflow template ID
        /// </summary>
        public Guid WorkflowTemplateId { get; set; }

        /// <summary>
        ///     Name of templated workflow
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     Templated workflow Configuration
        /// </summary>
        public ConfigurableObjectParameterCollection Configuration { get; set; }

        /// <summary>
        ///     Participant plugins for this templated workflow and their configuration mappings
        /// </summary>
        public List<WorkflowTemplateParticipantDefinition> Participants { get; set; } = new();

        public WorkflowTemplateDefinition Clone()
        {
            return new WorkflowTemplateDefinition
            {
                WorkflowTemplateId = WorkflowTemplateId,
                Name = Name,
                Configuration = Configuration,
                Participants = new List<WorkflowTemplateParticipantDefinition>(Participants)
            };
        }

        /*
         * ---===[ EXAMPLE TEMPLATE FROM STRING ]===---
         {
            "WorkflowTemplateId": "6d0129af-83fc-3e4e-920f-acda7a5426e8",
            "Name": "Reset Key Vault Key",
            "Configuration": {
                "Parameters": [
                    {
                        "Name": "ResourceGroup",
                        "ExpectedType": "System.String",
                        "Value": null,
                        "DisplayName": "Resource Group",
                        "Description": null,
                        "DefinesAuthorizationScope": true,
                        "Required": true
                    }
                ]
            },
            "Participants": [
                {
                    "WorkflowStepName": "Regenerate Key Vault Key",
                    "PackageId": "6d0129af-83fc-3e4e-920f-8e1bc11fb0a3",
                    "PackageType": null,
                    "ServiceConfiguration": null,
                    "ConfigurationMappings": {
                        "KeyName": "KeyName"
                    }
                }
            ]
        }
        */
        public static WorkflowTemplateDefinition FromString(string templateString) =>
            PluginSharedHelpers.GetFromSerializedString<WorkflowTemplateDefinition>(templateString);

        public WorkflowTemplateDefinition RunWorkflowStep<TParticipant>(
            params KeyValuePair<string, string>[] mappings) where TParticipant : IPlugin, new()
        {
            Participants.Add(
                new WorkflowTemplateParticipantDefinition<TParticipant>(mappings.ToDictionary(k => k.Key,
                    v => v.Value)));
            return this;
        }

        public WorkflowTemplateDefinition RunAnyChildWorkflows()
        {
            Participants.Add(new WorkflowTemplateParticipantDefinition());
            return this;
        }

        public override string ToString()
        {
            return $"Workflow '{Name}' ({Participants.Count})";
        }
    }

    public class WorkflowTemplateDefinition<TManagedService, TConfiguration> : WorkflowTemplateDefinition
        where TManagedService : IManagedServicePackage
        where TConfiguration : IConfigurableObjectConfiguration, new()
    {
        public WorkflowTemplateDefinition()
        {
            Configuration = ConfigurableObjectParameterCollection.CreateFromObject(new TConfiguration());
        }

        public WorkflowTemplateDefinition(string name) : this()
        {
            Name = name;
            WorkflowTemplateId = IdGenerationExtensions.GenerateWorkflowId<TManagedService>(name);
        }

        public WorkflowTemplateDefinition(string name,
            params WorkflowTemplateParticipantDefinition[] participantDefinitions) : this(name)
        {
            foreach (var item in participantDefinitions)
                Participants.Add(item);
        }
    }

    public static class WorkflowTemplateDefinitionExtensions
    {
        public static KeyValuePair<string, string> MapsTo(this string key, string value)
        {
            return new(key, value);
        }
    }
}