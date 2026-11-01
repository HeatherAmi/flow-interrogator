// FlowAction.cs
//
// A single action from a flow's JSON definition. Actions can be nested inside Condition,
// Switch and Scope actions in the source JSON; FlowParser flattens all of that into one
// dictionary keyed by action name, since Logic App action names are unique across the whole
// flow regardless of nesting.

using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace HeatherAmiDigital.FlowInterrogator.Core.Models;

public sealed class FlowAction
{
    public string Name { get; set; }
    public string Type { get; set; }
    public string Kind { get; set; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> RunAfter { get; set; }
    public JToken RawJson { get; set; }
}
