// LoginWindow.xaml.cs
using System.Windows;
using Microsoft.AspNetCore.SignalR.Client;

namespace Client
{
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

        // Asynchronously start the SignalR connection
        private async void ConnectToServer()
        {
            try
            {
                await connection.StartAsync();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Failed to connect to server: " + ex.Message);
            }
        }

        // Handle login button click event
        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameTextBox.Text;
            string password = PasswordBox.Password;

            try
            {
                // Invoke the Login method on the server
                bool isAuthenticated = await connection.InvokeAsync<bool>("Login", username, password);
                if (isAuthenticated)
                {
                    // Open MainWindow if authentication is successful
                    MainWindow mainWindow = new MainWindow();
                    mainWindow.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Invalid username or password.");
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("An error occurred: " + ex.Message);
            }
        }

        // Open the registration window
        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            RegisterWindow registerWindow = new RegisterWindow();
            registerWindow.Show();
            this.Close();
        }
    }
}
