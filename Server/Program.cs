using Server;

class Program
{
    static void Main(string[] args)
    {
        ChatServer server = new ChatServer();
        server.StartServer("127.0.0.1", 5000);

        // Keep server alive
        Console.WriteLine("Press ENTER to stop the server.");
        Console.ReadLine();
    }
}