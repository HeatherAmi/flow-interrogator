// FlowTrigger.cs
//
// A single trigger from a flow's JSON definition. A flow currently has exactly one trigger,
// but it's still keyed in a dictionary in the source JSON (and here), since that's how the
// flow's own schema stores it.

using Newtonsoft.Json.Linq;

namespace HeatherAmiDigital.FlowInterrogator.Core.Models;

public sealed class FlowTrigger
{
    public string Name { get; set; }
    public string Type { get; set; }
    public string Kind { get; set; }
    public JToken RawJson { get; set; }
}
