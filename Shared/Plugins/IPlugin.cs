public interface IPlugin
{
    // Unique name of the plugin.
    string Name { get; }

    // Initialize the plugin. Called after the plugin is loaded.
    void Initialize();

    // Execute the plugin functionality.
    void Execute();
}