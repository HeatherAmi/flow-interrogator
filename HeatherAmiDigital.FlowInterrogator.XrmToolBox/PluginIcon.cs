// PluginIcon.cs
//
// Holds the base64-encoded icon XrmToolBox displays for this plugin in its tool list
// and tool window. XrmToolBox reads these icons from plugin metadata (see
// FlowInterrogatorPlugin.cs) before it creates any actual plugin instance, so the
// value has to live somewhere it can be read as a compile-time constant.

namespace HeatherAmiDigital.FlowInterrogator.XrmToolBox;
internal static class PluginIcon
{
    public const string Base64 = 
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";
}