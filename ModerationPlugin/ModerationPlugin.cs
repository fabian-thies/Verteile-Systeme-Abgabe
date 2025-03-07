using System;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR.Client;

// This plugin enhances the existing chat by filtering insults from messages and processing useful chat commands.
// It implements the IPlugin interface and provides an additional method to process outgoing messages.
public class ChatEnhancementPlugin : IPlugin
{
    // The name of the plugin
    public string Name => "ChatEnhancementPlugin";

    // Initialize method for plugin-specific setup (none needed here)
    public void Initialize()
    {
        // No initialization required for background processing.
    }

    // Execute method is not used directly because this plugin works in the background.
    public void Execute()
    {
        // No UI to execute.
    }

    // Processes an outgoing chat message.
    // If the message is a command (starts with '/'), it executes the command and returns the result.
    // Otherwise, it filters out any insults from the message.
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

    // Processes a chat command and returns the resulting message.
    private string ProcessCommand(string message)
    {
        // Split the command and its arguments.
        var parts = message.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLowerInvariant();
        var args = parts.Length > 1 ? parts[1] : string.Empty;

        switch (command)
        {
            case "/roll":
                // Roll a dice (1 to 6)
                var rnd = new Random();
                int roll = rnd.Next(1, 7);
                return "You rolled: " + roll;

            case "/shrug":
                // Return a shrug emoticon
                return "¯\\_(ツ)_/¯";

            case "/time":
                // Return the current time
                return "Current time: " + DateTime.Now.ToString("HH:mm:ss");

            case "/help":
                // Return a list of available commands
                return "Available commands:\n" +
                       "/roll - Roll a dice\n" +
                       "/shrug - Get a shrug emoticon\n" +
                       "/time - Show current time\n" +
                       "/help - Show this help message";

            default:
                return "Unknown command. Type /help for available commands.";
        }
    }

    // Filters insults from the provided message by replacing each insult word with "[censored]".
    private string FilterInsults(string message)
    {
        string[] insults = { "idiot", "stupid", "dumb", "fool", "moron", "blöd", "dumm", "arschloch" };
        foreach (var insult in insults)
        {
            // Regex is used for whole word matching (case-insensitive).
            message = Regex.Replace(message, @"\b" + Regex.Escape(insult) + @"\b", "[censored]", RegexOptions.IgnoreCase);
        }
        return message;
    }
}
