using ChatFileApp.Models.Plugins;

namespace TestPlugin
{
    public class MyTestPlugin : IPlugin
    {
        public string Id => "test-plugin";
        public string Name => "Test Plugin";
        public string Version => "1.0.0";
        public string Description => "Ein Plugin zum Testen der Ladefunktionalität";

        public Task Initialize()
        {
            Console.WriteLine("Test Plugin wurde initialisiert");
            return Task.CompletedTask;
        }

        public Task Shutdown()
        {
            Console.WriteLine("Test Plugin wird heruntergefahren");
            return Task.CompletedTask;
        }
    }
}