// FlowParser.cs
//
// Turns the raw clientdata JSON string from a workflow row into a FlowDefinition: a trigger,
// and a flat list of actions. A flow's actions aren't actually flat in the JSON — an
// If/Switch/Scope action nests its own child actions inside itself — so this walks the tree
// recursively and collects every action it finds into one dictionary, regardless of how deeply
// nested it was. Action names are unique across the whole flow, so flattening them loses no
// information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using HeatherAmiDigital.FlowInterrogator.Core.Models;

namespace HeatherAmiDigital.FlowInterrogator.Core.Services;

public sealed class FlowParser
{
    public FlowDefinition ParseDefinition(Guid flowId, string rawJson)
    {
        var definition = new FlowDefinition
        {
            FlowId = flowId,
            Triggers = new Dictionary<string, FlowTrigger>(),
            Actions = new Dictionary<string, FlowAction>()
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
        definition.Triggers = ParseTriggers(definitionRoot);

        var actions = new Dictionary<string, FlowAction>();
        ExtractActionsRecursive(definitionRoot, actions);
        definition.Actions = actions;

        return definition;
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

                // An If, Switch or Scope action nests its own child actions inside itself,
                // so recurse into it to find them too.
                ExtractActionsRecursive(actionJson, collectedActions);
            }
        }

        // An If action's "false" branch.
        if (currentObj["else"] != null)
        {
            ExtractActionsRecursive(currentObj["else"], collectedActions);
        }

        // A Switch action's cases, and its default branch.
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
}
