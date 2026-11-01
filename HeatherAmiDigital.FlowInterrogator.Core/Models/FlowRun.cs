// FlowRun.cs
//
// A single execution (run) of a cloud flow, retrieved from the Power Automate Management API
// rather than Dataverse — flow run history isn't stored in Dataverse at all, it lives in the
// separate Power Automate service.

using System;

namespace HeatherAmiDigital.FlowInterrogator.Core.Models;

public enum FlowRunStatus
{
    Succeeded,
    Failed,
    Cancelled,
    Skipped,
    Aborted,
    Waiting,
    Unknown
}

public sealed class FlowRun
{
    // A string rather than a GUID, as dictated by the Logic Apps runtime (e.g. "08585...").
    public string RunId { get; set; }
    public Guid FlowId { get; set; }
    public string FlowName { get; set; }
    public FlowRunStatus Status { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string TriggerName { get; set; }
    public string CorrelationId { get; set; }
    public string EnvironmentId { get; set; }
    public string Error { get; set; }

    public string GetPortalUrl()
    {
        if (string.IsNullOrWhiteSpace(EnvironmentId))
        {
            return null;
        }

        return $"https://make.powerautomate.com/environments/{EnvironmentId}/flows/{FlowId}/runs/{RunId}";
    }
}
