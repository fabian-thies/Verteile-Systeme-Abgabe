// RegisterWindow.xaml.cs
// English comment: Handles registration functionality and displays error messages within the window.

using System.Windows;
using Microsoft.AspNetCore.SignalR.Client;

namespace Client.Views;

public partial class RegisterWindow : Window
{
    private HubConnection connection;

    public RegisterWindow()
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

    // English comment: Handles the registration button click event.
    private async void RegisterButton_Click(object sender, RoutedEventArgs e)
    {
        // Clear previous error messages
        ErrorTextBlock.Text = string.Empty;

        var username = UsernameTextBox.Text;
        var password = PasswordBox.Password;
        var confirmPassword = ConfirmPasswordBox.Password;

        if (password != confirmPassword)
        {
            ErrorTextBlock.Text = "Passwords do not match.";
            return;
        }

        try
        {
            // Invoke the Register method on the server
            var isRegistered = await connection.InvokeAsync<bool>("Register", username, password);
            if (isRegistered)
            {
                // Display success message (optional) or navigate back to login window.
                ErrorTextBlock.Text = "Registration successful!";
                var loginWindow = new LoginWindow();
                loginWindow.Show();
                Close();
            }
            else
            {
                ErrorTextBlock.Text = "Registration failed. Username might already exist.";
            }
        }
        catch (Exception ex)
        {
            ErrorTextBlock.Text = "An error occurred: " + ex.Message;
        }
    }

    // English comment: Navigates back to the login window.
    private void BackToLoginButton_Click(object sender, RoutedEventArgs e)
    {
        var loginWindow = new LoginWindow();
        loginWindow.Show();
        Close();
    }
}