public interface IPlugin
{
    string Name { get; }

    void Initialize();

    void Execute();
}