using System.Windows;
using Microsoft.AspNetCore.SignalR.Client;

namespace Client.Views;

public partial class LoginWindow : Window
{
    private readonly HubConnection connection;

    public LoginWindow()
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

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorTextBlock.Text = string.Empty;

        var username = UsernameTextBox.Text;
        var password = PasswordBox.Password;

        try
        {
            var isAuthenticated = await connection.InvokeAsync<bool>("Login", username, password);
            if (isAuthenticated)
            {
                var mainWindow = new MainWindow(connection);

                Application.Current.MainWindow = mainWindow;

                mainWindow.Show();
                this.Close();
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

    private void RegisterButton_Click(object sender, RoutedEventArgs e)
    {
        var registerWindow = new RegisterWindow();
        registerWindow.Show();
        Close();
    }
}