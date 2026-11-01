// FlowQueryService.cs
//
// Talks to Dataverse directly to get the list of cloud flows, the full definition of any
// single one, and to search across every flow's definition for a term. A flow is stored as a
// row in the standard workflow table, filtered to category 5 (a modern cloud flow, not a
// classic workflow or business rule) and type 1 (the actual flow, not the separate activation
// or template rows Dataverse also keeps for each one, those would otherwise make every flow
// appear two or three times in the list).

using System;
using System.Collections.Generic;
using System.Linq;
using HeatherAmiDigital.FlowInterrogator.Core.Models;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace HeatherAmiDigital.FlowInterrogator.Core.Services;

public sealed class FlowQueryService
{
    private const int PageSize = 5000;

    // Dataverse fault error code (0x80040217) returned when a requested record no longer exists.
    private const int ObjectDoesNotExistErrorCode = -2147220969;

    private readonly IOrganizationService _service;
    private readonly FlowParser _parser;

    public FlowQueryService(IOrganizationService service, FlowParser parser)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    public IReadOnlyList<FlowSummary> GetFlowSummaries()
    {
        var query = BuildFlowQuery(includeClientData: false);
        var summaries = new List<FlowSummary>();

        foreach (var entity in RetrieveAll(query, isCancellationRequested: null, onPageRetrieved: null))
        {
            summaries.Add(MapToSummary(entity));
        }

        return summaries
            .OrderByDescending(f => f.ModifiedOn)
            .ToList();
    }

    public FlowDefinition GetFlowDefinition(Guid flowId)
    {
        Entity entity;
        try
        {
            entity = _service.Retrieve("workflow", flowId, new ColumnSet("clientdata"));
        }
        catch (Exception ex) when (IsObjectNotFoundFault(ex))
        {
            return null;
        }

        var rawJson = entity.GetAttributeValue<string>("clientdata");
        return _parser.ParseDefinition(flowId, rawJson);
    }

    // Searches every flow's definition for a term. Fetches the clientdata column too, so
    // results are paged to stay within Dataverse's response size cap on large environments.
    public IReadOnlyList<FlowMatch> SearchFlowDefinitions(
        string searchTerm,
        Action<int> onPageRetrieved = null,
        Func<bool> isCancellationRequested = null)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return Array.Empty<FlowMatch>();
        }

        var query = BuildFlowQuery(includeClientData: true);
        var matches = new List<FlowMatch>();

        foreach (var entity in RetrieveAll(query, isCancellationRequested, onPageRetrieved))
        {
            var summary = MapToSummary(entity);
            var rawJson = entity.GetAttributeValue<string>("clientdata");

            if (string.IsNullOrWhiteSpace(rawJson)) continue;

            var definition = _parser.ParseDefinition(summary.Id, rawJson);
            matches.AddRange(_parser.SearchDefinition(summary, definition, searchTerm));
        }

        return matches;
    }

    // Determines whether an exception is a Dataverse "object does not exist" fault (error code
    // 0x80040217), without taking a compile-time dependency on
    // FaultException<OrganizationServiceFault>, which isn't reliably present in the XrmToolBox
    // host. The fault detail is inspected via reflection instead.
    private static bool IsObjectNotFoundFault(Exception ex)
    {
        var detail = ex.GetType().GetProperty("Detail")?.GetValue(ex);
        var errorCode = detail?.GetType().GetProperty("ErrorCode")?.GetValue(detail);
        return errorCode is int code && code == ObjectDoesNotExistErrorCode;
    }

    private static QueryExpression BuildFlowQuery(bool includeClientData)
    {
        var columns = new List<string>
        {
            "workflowid", "name", "description", "statecode",
            "createdon", "modifiedon", "ownerid"
        };

        if (includeClientData)
        {
            columns.Add("clientdata");
        }

        var query = new QueryExpression("workflow")
        {
            NoLock = true,
            ColumnSet = new ColumnSet(columns.ToArray()),
            PageInfo = new PagingInfo { Count = PageSize, PageNumber = 1 }
        };

        query.Criteria.AddCondition("category", ConditionOperator.Equal, 5);
        query.Criteria.AddCondition("type", ConditionOperator.Equal, 1);

        return query;
    }

    // Executes a paged retrieval, yielding every matching entity across all pages. Stops early
    // if isCancellationRequested returns true — nothing wires a real cancel button into this
    // yet (that's Stage 8), but the hook is here so that stage doesn't need to touch this code.
    private IEnumerable<Entity> RetrieveAll(
        QueryExpression query,
        Func<bool> isCancellationRequested,
        Action<int> onPageRetrieved)
    {
        while (true)
        {
            if (isCancellationRequested?.Invoke() == true)
            {
                yield break;
            }

            var results = _service.RetrieveMultiple(query);

            foreach (var entity in results.Entities)
            {
                yield return entity;
            }

            onPageRetrieved?.Invoke(query.PageInfo.PageNumber);

            if (!results.MoreRecords)
            {
                yield break;
            }

            query.PageInfo.PageNumber++;
            query.PageInfo.PagingCookie = results.PagingCookie;
        }
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
}
