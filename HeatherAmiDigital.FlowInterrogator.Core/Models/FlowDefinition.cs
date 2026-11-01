// FlowDefinition.cs
//
// The parsed execution definition of one cloud flow: its triggers and actions, extracted from
// the clientdata JSON column. RawJson keeps the whole thing too, so the UI can still show it
// even once it's been broken apart into FlowTrigger/FlowAction.

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace HeatherAmiDigital.FlowInterrogator.Core.Models;

public sealed class FlowDefinition
{
    public Guid FlowId { get; set; }
    public string Schema { get; set; }
    public string ContentVersion { get; set; }
    public IReadOnlyDictionary<string, FlowTrigger> Triggers { get; set; }
    public IReadOnlyDictionary<string, FlowAction> Actions { get; set; }
    public JToken RawJson { get; set; }
}
