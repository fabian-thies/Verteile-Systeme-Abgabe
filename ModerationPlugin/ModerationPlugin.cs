using System;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR.Client;

public class ChatEnhancementPlugin : IPlugin
{
    public string Name => "ChatEnhancementPlugin";

    public void Initialize()
    {
    }

    public void Execute()
    {
    }

    public string ProcessMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        if (message.StartsWith("/"))
        {
            return ProcessCommand(message);
        }
        else
        {
            return FilterInsults(message);
        }
    }

    private string ProcessCommand(string message)
    {
        var parts = message.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLowerInvariant();
        var args = parts.Length > 1 ? parts[1] : string.Empty;

        switch (command)
        {
            case "/roll":
                var rnd = new Random();
                int roll = rnd.Next(1, 7);
                return "You rolled: " + roll;

            case "/shrug":
                return "¯\\_(ツ)_/¯";

            case "/time":
                return "Current time: " + DateTime.Now.ToString("HH:mm:ss");

            case "/help":
                return "Available commands:\n" +
                       "/roll - Roll a dice\n" +
                       "/shrug - Get a shrug emoticon\n" +
                       "/time - Show current time\n" +
                       "/help - Show this help message";

            default:
                return "Unknown command. Type /help for available commands.";
        }
    }

    private string FilterInsults(string message)
    {
        string[] insults = { "idiot", "stupid", "dumb", "fool", "moron", "blöd", "dumm", "arschloch" };
        foreach (var insult in insults)
        {
            message = Regex.Replace(message, @"\b" + Regex.Escape(insult) + @"\b", "[censored]", RegexOptions.IgnoreCase);
        }
        return message;
    }
}
