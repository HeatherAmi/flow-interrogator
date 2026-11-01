// FlowState.cs
//
// Represents whether a flow is a draft, turned on, or turned off. Matches the
// statecode option set on the Dataverse workflow table exactly, so these numbers are
// not arbitrary, they come directly from Dataverse.

namespace HeatherAmiDigital.FlowInterrogator.Core.Models;

public enum FlowState
{
    Draft = 0,
    Activated = 1,
    Suspended = 2
}
