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
//     the hood a PluginControlBase is just a WinForms UserControl, so everything you
//     would normally do with a UserControl (add child controls, handle events) applies
//     here directly.



using System.ComponentModel.Composition;
using System.Drawing;
using System.Windows.Forms;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace HeatherAmiDigital.FlowInterrogator.XrmToolBox;

[Export(typeof(IXrmToolBoxPlugin)),
ExportMetadata("Name", "Flow Interrogator"),
ExportMetadata("Description", "Search and investigate power automate cloud flows and run history"),
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
    public FlowInterrogatorPlugin()
    {
        var label = new Label
        {
            Text = "Hello, Flow Interrogator!",
            AutoSize = true,
            Font = new Font("Segoe UI", 14),
            Top = 20,
            Left = 20
        };
        Controls.Add(label);

    }
        
}