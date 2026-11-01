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
//     connection becomes available or changes, that is where we build a
//     FlowQueryService against the live connection and hand it to MainControl.

using System.ComponentModel.Composition;
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

    public FlowInterrogatorPlugin()
    {
        _mainControl = new MainControl();
        Controls.Add(_mainControl);
    }

    public override void UpdateConnection(IOrganizationService newService, ConnectionDetail detail, string actionName, object parameter)
    {
        base.UpdateConnection(newService, detail, actionName, parameter);

        var queryService = new FlowQueryService(Service, new FlowParser());
        _mainControl.SetQueryService(queryService);
    }
}
