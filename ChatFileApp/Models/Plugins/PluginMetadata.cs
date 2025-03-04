// Models/Plugins/PluginMetadata.cs
namespace ChatFileApp.Models.Plugins
{
    public class PluginMetadata
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public string AssemblyPath { get; set; }
        public string EntryPoint { get; set; }
        public byte[] Signature { get; set; }
        public string PublicKeyToken { get; set; }
    }
}