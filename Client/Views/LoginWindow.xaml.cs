// LoginWindow.xaml.cs
// English comment: Handles login functionality and displays error messages within the window.

using System.Windows;
using Microsoft.AspNetCore.SignalR.Client;

namespace Client.Views;

public partial class LoginWindow : Window
{
    private HubConnection connection;

    public LoginWindow()
    {
        InitializeComponent();
        // Initialize connection to the SignalR hub on the server
        connection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5000/chatHub") // Adjust the URL and port as needed
            .Build();

        // Start the connection asynchronously
        ConnectToServer();
    }

    // English comment: Asynchronously starts the SignalR connection.
    private async void ConnectToServer()
    {
        try
        {
            await connection.StartAsync();
        }
        catch (Exception ex)
        {
            ErrorTextBlock.Text = "Failed to connect to server: " + ex.Message;
        }
    }

    // English comment: Handles the login button click event.
    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        // Clear previous error messages
        ErrorTextBlock.Text = string.Empty;

        var username = UsernameTextBox.Text;
        var password = PasswordBox.Password;

        try
        {
            // Invoke the Login method on the server
            var isAuthenticated = await connection.InvokeAsync<bool>("Login", username, password);
            if (isAuthenticated)
            {
                // Open MainWindow if authentication is successful
                var mainWindow = new MainWindow();
                mainWindow.Show();
                Close();
            }
            else
            {
                ErrorTextBlock.Text = "Invalid username or password.";
            }
        }
        catch (Exception ex)
        {
            ErrorTextBlock.Text = "An error occurred: " + ex.Message;
        }
    }

    // English comment: Opens the registration window.
    private void RegisterButton_Click(object sender, RoutedEventArgs e)
    {
        var registerWindow = new RegisterWindow();
        registerWindow.Show();
        Close();
    }
}