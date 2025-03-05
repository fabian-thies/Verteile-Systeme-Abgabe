using System.Windows;
using Microsoft.AspNetCore.SignalR.Client;

namespace Client.Views;

public partial class RegisterWindow : Window
{
    private HubConnection connection;

    public RegisterWindow()
    {
        InitializeComponent();
        connection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5000/chatHub")
            .Build();

        ConnectToServer();
    }

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

    private async void RegisterButton_Click(object sender, RoutedEventArgs e)
    {
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
            var isRegistered = await connection.InvokeAsync<bool>("Register", username, password);
            if (isRegistered)
            {
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

    private void BackToLoginButton_Click(object sender, RoutedEventArgs e)
    {
        var loginWindow = new LoginWindow();
        loginWindow.Show();
        Close();
    }
}