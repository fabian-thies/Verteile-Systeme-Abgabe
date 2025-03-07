using System.Windows;
using System.Windows.Input;
using Microsoft.AspNetCore.SignalR.Client;

namespace Client.Views;

public partial class RegisterWindow : Window
{
    private readonly HubConnection connection;

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
            // Clear error message and hide error panel on success
            ErrorTextBlock.Text = "";
            ErrorPanel.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ErrorTextBlock.Text = "Failed to connect to server: " + ex.Message;
            ErrorPanel.Visibility = Visibility.Visible;
        }
    }

    private async void RegisterButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorTextBlock.Text = "";
        ErrorPanel.Visibility = Visibility.Collapsed;

        var username = UsernameTextBox.Text;
        var password = PasswordBox.Password;
        var confirmPassword = ConfirmPasswordBox.Password;

        if (password != confirmPassword)
        {
            ErrorTextBlock.Text = "Passwords do not match.";
            ErrorPanel.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var isRegistered = await connection.InvokeAsync<bool>("Register", username, password);
            if (isRegistered)
            {
                // Optionally show a brief success message before navigating back to login
                ErrorTextBlock.Text = "Registration successful!";
                ErrorPanel.Visibility = Visibility.Visible;
                var loginWindow = new LoginWindow();
                loginWindow.Show();
                Close();
            }
            else
            {
                ErrorTextBlock.Text = "Registration failed. Username might already exist.";
                ErrorPanel.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            ErrorTextBlock.Text = "An error occurred: " + ex.Message;
            ErrorPanel.Visibility = Visibility.Visible;
        }
    }

    private void BackToLoginButton_Click(object sender, RoutedEventArgs e)
    {
        var loginWindow = new LoginWindow();
        loginWindow.Show();
        Close();
    }

    // Allows dragging of the window from the title bar.
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    // Closes the window when the close button is clicked.
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}