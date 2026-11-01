// FlowRunService.cs
//
// Talks to the Power Automate Management API to list the run history of a single flow. This
// stage only lists runs — the richer per-run action detail (actual inputs/outputs, following
// blob links for large payloads) is deliberately left for a later stage, since that needs its
// own careful handling and isn't needed just to see what happened and when.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using HeatherAmiDigital.FlowInterrogator.Core.Models;
using Newtonsoft.Json.Linq;

namespace HeatherAmiDigital.FlowInterrogator.Core.Services;

public sealed class FlowRunService
{
    private const string ApiVersion = "2016-11-01";
    private const string BaseUrl = "https://api.flow.microsoft.com";

    // Busy flows can have thousands of runs; this caps the work (~1000 runs at the API's
    // default page size) so a single flow can't stall the UI indefinitely.
    private const int MaxRunPages = 5;

    private readonly HttpClient _httpClient;
    private readonly ITokenProvider _tokenProvider;
    private string _resolvedEnvironmentId;

    // The Dataverse instance (web application) URL of the connected organisation, e.g.
    // https://contoso.crm.dynamics.com/. The Flow API needs its own environment name, a
    // different ID scheme than Dataverse's own environment ID, so this is resolved by matching
    // this URL against what the Flow API reports.
    public string InstanceUrl { get; set; }

    public FlowRunService(HttpClient httpClient, ITokenProvider tokenProvider)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
    }

    public async Task<IReadOnlyList<FlowRun>> GetRunsAsync(Guid flowId, string flowName)
    {
        var environmentId = await ResolveEnvironmentIdAsync();

        var url = $"{BaseUrl}/providers/Microsoft.ProcessSimple/environments/{environmentId}/flows/{flowId}/runs?api-version={ApiVersion}";

        var runs = new List<FlowRun>();
        var nextUrl = url;
        var page = 0;

        // The Flow API pages run history via @odata.nextLink. Follow it up to MaxRunPages so
        // busy flows return runs beyond the first page without an unbounded crawl.
        while (!string.IsNullOrEmpty(nextUrl) && page < MaxRunPages)
        {
            var json = await SendAuthenticatedRequestAsync(nextUrl);

            if (json["value"] is JArray runsArray)
            {
                foreach (var runToken in runsArray)
                {
                    runs.Add(MapToFlowRun(runToken, flowId, flowName, environmentId));
                }
            }

            page++;
            nextUrl = json["@odata.nextLink"]?.ToString();
        }

        return runs.OrderByDescending(r => r.StartTime).ToList();
    }

    // Resolves the Power Platform environment name the Flow API expects, by matching
    // InstanceUrl against the environments it reports. Cached for the lifetime of the service.
    private async Task<string> ResolveEnvironmentIdAsync()
    {
        if (!string.IsNullOrWhiteSpace(_resolvedEnvironmentId))
        {
            return _resolvedEnvironmentId;
        }

        if (string.IsNullOrWhiteSpace(InstanceUrl) || !Uri.TryCreate(InstanceUrl, UriKind.Absolute, out var instanceUri))
        {
            throw new InvalidOperationException("InstanceUrl must be set before fetching run history.");
        }

        var url = $"{BaseUrl}/providers/Microsoft.ProcessSimple/environments?api-version={ApiVersion}";
        var json = await SendAuthenticatedRequestAsync(url);

        if (json["value"] is JArray environments)
        {
            foreach (var env in environments)
            {
                var meta = env["properties"]?["linkedEnvironmentMetadata"];
                var linkedUrl = meta?["instanceUrl"]?.ToString() ?? meta?["instanceApiUrl"]?.ToString();

                if (!string.IsNullOrWhiteSpace(linkedUrl)
                    && Uri.TryCreate(linkedUrl, UriKind.Absolute, out var linkedUri)
                    && string.Equals(linkedUri.Host, instanceUri.Host, StringComparison.OrdinalIgnoreCase))
                {
                    _resolvedEnvironmentId = env["name"]?.ToString();
                    return _resolvedEnvironmentId;
                }
            }
        }

        throw new InvalidOperationException($"No Power Automate environment matched '{instanceUri.Host}'.");
    }

    private async Task<JObject> SendAuthenticatedRequestAsync(string url)
    {
        var token = await _tokenProvider.GetAccessTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Flow API request failed with status {(int)response.StatusCode}: {errorContent}");
        }

        var content = await response.Content.ReadAsStringAsync();
        return JObject.Parse(content);
    }

    private static FlowRun MapToFlowRun(JToken runToken, Guid flowId, string flowName, string environmentId)
    {
        var props = runToken["properties"];
        var correlation = props?["correlation"];

        return new FlowRun
        {
            RunId = runToken["name"]?.ToString(),
            FlowId = flowId,
            FlowName = flowName,
            EnvironmentId = environmentId,
            Status = ParseStatus(props?["status"]?.ToString()),
            StartTime = props?["startTime"]?.ToObject<DateTime>() ?? DateTime.MinValue,
            EndTime = props?["endTime"]?.ToObject<DateTime?>(),
            TriggerName = props?["trigger"]?["name"]?.ToString(),
            CorrelationId = correlation?["clientTrackingId"]?.ToString(),
            Error = props?["error"]?["message"]?.ToString()
        };
    }

    private static FlowRunStatus ParseStatus(string statusString)
    {
        if (string.IsNullOrWhiteSpace(statusString))
        {
            return FlowRunStatus.Unknown;
        }

        return Enum.TryParse<FlowRunStatus>(statusString, true, out var status)
            ? status
            : FlowRunStatus.Unknown;
    }
}
