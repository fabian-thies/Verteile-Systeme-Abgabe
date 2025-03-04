// WhiteboardPlugin/WhiteboardHandler.cs
using ChatFileApp.Models.Plugins;

namespace WhiteboardPlugin
{
    public class WhiteboardHandler : IPlugin
    {
        public string Id => "whiteboard-plugin";
        public string Name => "Whiteboard Plugin";
        public string Version => "1.0.0";
        public string Description => "A live collaborative whiteboard plugin";

        public Task Initialize()
        {
            // This is called once after the plugin is loaded.
            // Here you might add any logic needed to initialize resources or data.
            Console.WriteLine("Whiteboard Plugin initialized.");
            return Task.CompletedTask;
        }

        public Task Shutdown()
        {
            // Clean up any resources if needed
            Console.WriteLine("Whiteboard Plugin shutting down.");
            return Task.CompletedTask;
        }
    }
}