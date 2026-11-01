// FlowQueryService.cs
//
// Talks to Dataverse directly to get the list of cloud flows. A flow is stored as a
// row in the standard workflow table, filtered to category 5 (a modern cloud flow, not
// a classic workflow or business rule) and type 1 (the actual flow, not the separate
// activation or template rows Dataverse also keeps for each one, those would otherwise
// make every flow appear two or three times in the list).

using System;
using System.Collections.Generic;
using System.Linq;
using HeatherAmiDigital.FlowInterrogator.Core.Models;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace HeatherAmiDigital.FlowInterrogator.Core.Services;

public sealed class FlowQueryService
{
    private readonly IOrganizationService _service;

    public FlowQueryService(IOrganizationService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public IReadOnlyList<FlowSummary> GetFlowSummaries()
    {
        var query = new QueryExpression("workflow")
        {
            ColumnSet = new ColumnSet("workflowid", "name", "description", "statecode", "createdon", "modifiedon", "ownerid"),
            NoLock = true
        };

        query.Criteria.AddCondition("category", ConditionOperator.Equal, 5);
        query.Criteria.AddCondition("type", ConditionOperator.Equal, 1);

        var result = _service.RetrieveMultiple(query);

        return result.Entities
            .Select(MapToSummary)
            .OrderByDescending(f => f.ModifiedOn)
            .ToList();
    }

    private static FlowSummary MapToSummary(Entity entity)
    {
        return new FlowSummary
        {
            Id = entity.Id,
            Name = entity.GetAttributeValue<string>("name"),
            Description = entity.GetAttributeValue<string>("description"),
            State = (FlowState)(entity.GetAttributeValue<OptionSetValue>("statecode")?.Value ?? 0),
            CreatedOn = entity.GetAttributeValue<DateTime?>("createdon"),
            ModifiedOn = entity.GetAttributeValue<DateTime?>("modifiedon"),
            OwnerName = entity.GetAttributeValue<EntityReference>("ownerid")?.Name
        };
    }
        public string GetFlowDefinitionJson(Guid flowId)
    {
        var entity = _service.Retrieve("workflow", flowId, new ColumnSet("clientdata"));
        var rawJson = entity.GetAttributeValue<string>("clientdata");

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return "(this flow has no saved definition)";
        }

        try
        {
            return JToken.Parse(rawJson).ToString(Formatting.Indented);
        }
        catch (JsonReaderException)
        {
            return rawJson;
        }
    }

}
