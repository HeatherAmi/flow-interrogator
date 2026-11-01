// FlowInterrogatorPlugin.cs
//
// This is the file XrmToolBox actually talks to. It has two classes, and the split
// matters.
//
//   - FlowInterrogatorPluginFactory is what XrmToolBox's plugin discovery (MEF) finds
//     when it scans this DLL. It never does any real work itself. It just carries
//     metadata (name, description, colours, icon) that XrmToolBox needs to list the
//     tool, and knows how to build the real thing via GetControl(), but only if and
//     when the user actually opens it.
//
//   - FlowInterrogatorPlugin is the real thing: the actual screen the user sees. Under
//     the hood a PluginControlBase is just a WinForms UserControl. It now also
//     overrides UpdateConnection, which XrmToolBox calls whenever a Dataverse
//     connection becomes available or changes — that's where we build a
//     FlowQueryService (against the live Dataverse connection) and a FlowRunService
//     (against the separate Power Automate Management API, with its own MSAL sign-in)
//     and hand both to MainControl. The HttpClient backing FlowRunService is created
//     once and reused for the plugin's lifetime, then disposed when XrmToolBox closes
//     the tool.

using System.ComponentModel.Composition;
using System.Net.Http;
using HeatherAmiDigital.FlowInterrogator.Core.Services;
using McTools.Xrm.Connection;
using Microsoft.Xrm.Sdk;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace HeatherAmiDigital.FlowInterrogator.XrmToolBox;

[Export(typeof(IXrmToolBoxPlugin)),
 ExportMetadata("Name", "Flow Interrogator"),
 ExportMetadata("Description", "Search and investigate Power Automate cloud flows and run history."),
 ExportMetadata("BackgroundColor", "White"),
 ExportMetadata("PrimaryFontColor", "#333333"),
 ExportMetadata("SecondaryFontColor", "#666666"),
 ExportMetadata("SmallImageBase64", PluginIcon.Base64),
 ExportMetadata("BigImageBase64", PluginIcon.Base64)]
public sealed class FlowInterrogatorPluginFactory : PluginBase
{
    public override IXrmToolBoxPluginControl GetControl() => new FlowInterrogatorPlugin();
}

public sealed class FlowInterrogatorPlugin : PluginControlBase
{
    private readonly MainControl _mainControl;
    private HttpClient _httpClient;

    public FlowInterrogatorPlugin()
    {
        _mainControl = new MainControl();
        Controls.Add(_mainControl);

        OnCloseTool += (sender, e) => _httpClient?.Dispose();
    }

    public override void UpdateConnection(IOrganizationService newService, ConnectionDetail detail, string actionName, object parameter)
    {
        base.UpdateConnection(newService, detail, actionName, parameter);

        var queryService = new FlowQueryService(Service, new FlowParser());

        var authService = new PowerAutomateAuthService();
        _httpClient ??= new HttpClient();
        var runService = new FlowRunService(_httpClient, authService)
        {
            // Lets FlowRunService resolve the correct Power Automate environment by matching
            // this Dataverse instance's URL, since the Flow API uses a different ID scheme
            // than Dataverse's own environment ID.
            InstanceUrl = detail?.WebApplicationUrl
        };

        _mainControl.SetServices(queryService, runService);
    }
}
