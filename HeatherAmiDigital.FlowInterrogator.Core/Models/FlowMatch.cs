// FlowMatch.cs
//
// A single search hit within a flow's definition — which flow, which specific part of it
// (a trigger, an action, a connection reference, or just "somewhere in the raw JSON" as a
// fallback), and a short snippet of text around the match so the result means something
// without having to open the flow.

using System;

namespace HeatherAmiDigital.FlowInterrogator.Core.Models;

public enum FlowMatchLocation
{
    Parameter,
    Trigger,
    Action,
    ConnectionReference,
    RawDefinition
}

public sealed class FlowMatch
{
    public Guid FlowId { get; set; }
    public string FlowName { get; set; }
    public FlowMatchLocation Location { get; set; }
    public string NodeName { get; set; }
    public string MatchSnippet { get; set; }
}
