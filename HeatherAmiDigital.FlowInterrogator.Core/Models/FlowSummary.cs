// FlowSummary.cs
//
// A lightweight, read-only view of one cloud flow, enough to show it in a list. This
// deliberately leaves out the flow's actual definition JSON, that is much heavier to
// fetch.

using System;

namespace HeatherAmiDigital.FlowInterrogator.Core.Models;

public sealed class FlowSummary
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public FlowState State { get; set; }
    public DateTime? CreatedOn { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public string OwnerName { get; set; }
}
