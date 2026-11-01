// FlowParser.cs
//
// Turns the raw clientdata JSON string from a workflow row into a FlowDefinition: its
// trigger, a flat list of actions, its connection references, and (via SearchDefinition) the
// ability to find a term anywhere within all of that.
//
// A flow's actions aren't actually flat in the JSON — an If/Switch/Scope action nests its own
// child actions inside itself — so ExtractActionsRecursive walks the tree and collects every
// action it finds into one dictionary, regardless of how deeply nested it was. Action names are
// unique across the whole flow, so flattening them loses no information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using HeatherAmiDigital.FlowInterrogator.Core.Models;

namespace HeatherAmiDigital.FlowInterrogator.Core.Services;

public sealed class FlowParser
{
    private const int SnippetContextLength = 40;

    public FlowDefinition ParseDefinition(Guid flowId, string rawJson)
    {
        var definition = new FlowDefinition
        {
            FlowId = flowId,
            Triggers = new Dictionary<string, FlowTrigger>(),
            Actions = new Dictionary<string, FlowAction>(),
            ConnectionReferences = new Dictionary<string, JToken>()
        };

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return definition;
        }

        JToken rootToken;
        try
        {
            rootToken = JToken.Parse(rawJson);
        }
        catch (JsonException)
        {
            definition.RawJson = JValue.CreateString(rawJson);
            return definition;
        }

        definition.RawJson = rootToken;
        definition.ConnectionReferences = ParseConnectionReferences(rootToken);

        // clientdata wraps the actual flow definition under properties.definition.
        var definitionRoot = (rootToken["properties"]?["definition"] as JObject)
            ?? (rootToken["definition"] as JObject)
            ?? rootToken as JObject;

        if (definitionRoot == null)
        {
            return definition;
        }

        definition.Schema = definitionRoot["$schema"]?.ToString();
        definition.ContentVersion = definitionRoot["contentVersion"]?.ToString();
        definition.Parameters = definitionRoot["parameters"];
        definition.Triggers = ParseTriggers(definitionRoot);

        var actions = new Dictionary<string, FlowAction>();
        ExtractActionsRecursive(definitionRoot, actions);
        definition.Actions = actions;

        return definition;
    }

    // Connection references live at properties.connectionReferences (or a top-level
    // connectionReferences), a section of clientdata that sits alongside the Logic App
    // definition rather than inside it, so they're not reached by the trigger/action walk.
    private Dictionary<string, JToken> ParseConnectionReferences(JToken rootToken)
    {
        var references = new Dictionary<string, JToken>();

        var connectionRefs = (rootToken["properties"]?["connectionReferences"] as JObject)
            ?? (rootToken["connectionReferences"] as JObject);
        if (connectionRefs == null) return references;

        foreach (var prop in connectionRefs.Properties())
        {
            references[prop.Name] = prop.Value;
        }

        return references;
    }

    private Dictionary<string, FlowTrigger> ParseTriggers(JObject definitionRoot)
    {
        var triggers = new Dictionary<string, FlowTrigger>();

        if (definitionRoot["triggers"] is not JObject triggersObj)
        {
            return triggers;
        }

        foreach (var prop in triggersObj.Properties())
        {
            if (prop.Value is not JObject triggerJson) continue;

            triggers[prop.Name] = new FlowTrigger
            {
                Name = prop.Name,
                Type = triggerJson["type"]?.ToString(),
                Kind = triggerJson["kind"]?.ToString(),
                RawJson = triggerJson
            };
        }

        return triggers;
    }

    private void ExtractActionsRecursive(JToken currentToken, Dictionary<string, FlowAction> collectedActions)
    {
        if (currentToken is not JObject currentObj) return;

        if (currentObj["actions"] is JObject actionsObj)
        {
            foreach (var actionProp in actionsObj.Properties())
            {
                if (actionProp.Value is not JObject actionJson) continue;

                collectedActions[actionProp.Name] = ParseAction(actionProp.Name, actionJson);
                ExtractActionsRecursive(actionJson, collectedActions);
            }
        }

        if (currentObj["else"] != null)
        {
            ExtractActionsRecursive(currentObj["else"], collectedActions);
        }

        if (currentObj["cases"] is JObject casesObj)
        {
            foreach (var caseProp in casesObj.Properties())
            {
                ExtractActionsRecursive(caseProp.Value, collectedActions);
            }
        }

        if (currentObj["default"] != null)
        {
            ExtractActionsRecursive(currentObj["default"], collectedActions);
        }
    }

    private FlowAction ParseAction(string name, JObject actionJson)
    {
        var runAfter = new Dictionary<string, IReadOnlyList<string>>();
        if (actionJson["runAfter"] is JObject runAfterObj)
        {
            foreach (var prop in runAfterObj.Properties())
            {
                runAfter[prop.Name] = prop.Value.ToObject<List<string>>() ?? new List<string>();
            }
        }

        return new FlowAction
        {
            Name = name,
            Type = actionJson["type"]?.ToString(),
            Kind = actionJson["kind"]?.ToString(),
            RunAfter = runAfter,
            RawJson = actionJson
        };
    }

    // Searches a parsed flow definition for a term (e.g. a GUID, email address, or URL),
    // across its parameters, triggers, actions and connection references, with a raw-JSON
    // fallback so a hit never goes unreported just because it landed somewhere unmapped.
    public IReadOnlyList<FlowMatch> SearchDefinition(FlowSummary summary, FlowDefinition definition, string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm) || definition?.RawJson == null)
        {
            return Array.Empty<FlowMatch>();
        }

        var matches = new List<FlowMatch>();
        var lowerSearchTerm = searchTerm.ToLowerInvariant();

        if (definition.Parameters != null && ContainsTerm(definition.Parameters.ToString(), lowerSearchTerm))
        {
            matches.Add(CreateMatch(summary, FlowMatchLocation.Parameter, "Parameters", definition.Parameters.ToString(), lowerSearchTerm));
        }

        foreach (var trigger in definition.Triggers.Values)
        {
            var triggerJson = trigger.RawJson?.ToString() ?? string.Empty;
            if (ContainsTerm(triggerJson, lowerSearchTerm))
            {
                matches.Add(CreateMatch(summary, FlowMatchLocation.Trigger, trigger.Name, triggerJson, lowerSearchTerm));
            }
        }

        foreach (var action in definition.Actions.Values)
        {
            var actionJson = action.RawJson?.ToString() ?? string.Empty;
            if (ContainsTerm(actionJson, lowerSearchTerm))
            {
                matches.Add(CreateMatch(summary, FlowMatchLocation.Action, action.Name, actionJson, lowerSearchTerm));
            }
        }

        foreach (var reference in definition.ConnectionReferences ?? new Dictionary<string, JToken>())
        {
            var referenceJson = reference.Value?.ToString() ?? string.Empty;
            if (ContainsTerm(referenceJson, lowerSearchTerm))
            {
                matches.Add(CreateMatch(summary, FlowMatchLocation.ConnectionReference, reference.Key, referenceJson, lowerSearchTerm));
            }
        }

        // Fallback: if nothing specific matched but the raw JSON did, report a raw-definition
        // match whose NodeName is the JSON path to the first hit, so the column is never blank.
        if (matches.Count == 0 && ContainsTerm(definition.RawJson.ToString(), lowerSearchTerm))
        {
            var path = FindJsonPath(definition.RawJson, lowerSearchTerm);
            matches.Add(CreateMatch(summary, FlowMatchLocation.RawDefinition, path, definition.RawJson.ToString(), lowerSearchTerm));
        }

        return matches;
    }

    private bool ContainsTerm(string text, string lowerSearchTerm)
    {
        return text != null && text.IndexOf(lowerSearchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // Walks the definition tree and returns the JSON path of the first node whose property
    // name or scalar value contains the search term, e.g. properties.connectionReferences.shared_x.
    private static string FindJsonPath(JToken root, string lowerSearchTerm)
    {
        if (root is not JContainer container) return null;

        foreach (var token in container.DescendantsAndSelf())
        {
            switch (token)
            {
                case JProperty prop when prop.Name.IndexOf(lowerSearchTerm, StringComparison.OrdinalIgnoreCase) >= 0:
                    return prop.Path;
                case JValue value when value.Value != null
                    && value.ToString().IndexOf(lowerSearchTerm, StringComparison.OrdinalIgnoreCase) >= 0:
                    return value.Path;
            }
        }

        return null;
    }

    private FlowMatch CreateMatch(FlowSummary summary, FlowMatchLocation location, string nodeName, string jsonContext, string lowerSearchTerm)
    {
        return new FlowMatch
        {
            FlowId = summary.Id,
            FlowName = summary.Name,
            Location = location,
            NodeName = nodeName,
            MatchSnippet = ExtractSnippet(jsonContext, lowerSearchTerm)
        };
    }

    // Extracts a short text snippet centred around the first occurrence of the search term.
    private string ExtractSnippet(string text, string lowerSearchTerm)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var index = text.IndexOf(lowerSearchTerm, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return string.Empty;

        var start = Math.Max(0, index - SnippetContextLength);
        var length = Math.Min(text.Length - start, lowerSearchTerm.Length + (SnippetContextLength * 2));

        var snippet = text.Substring(start, length).Replace("\r", " ").Replace("\n", " ");

        if (start > 0) snippet = "..." + snippet;
        if (start + length < text.Length) snippet += "...";

        return snippet;
    }
}
