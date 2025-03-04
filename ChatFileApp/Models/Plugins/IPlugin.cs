// Models/Plugins/IPlugin.cs
namespace ChatFileApp.Models.Plugins
{
    public interface IPlugin
    {
        string Id { get; }
        string Name { get; }
        string Version { get; }
        string Description { get; }
        Task Initialize();
        Task Shutdown();
    }
}