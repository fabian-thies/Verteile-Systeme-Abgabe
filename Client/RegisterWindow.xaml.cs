// RegisterWindow.xaml.cs
using System.Windows;
using Microsoft.AspNetCore.SignalR.Client;
using System.Threading.Tasks;

namespace Client
{
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

        // Handle register button click event
        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameTextBox.Text;
            string password = PasswordBox.Password;
            string confirmPassword = ConfirmPasswordBox.Password;

            if (password != confirmPassword)
            {
                MessageBox.Show("Passwords do not match.");
                return;
            }

            try
            {
                // Invoke the Register method on the server
                bool isRegistered = await connection.InvokeAsync<bool>("Register", username, password);
                if (isRegistered)
                {
                    MessageBox.Show("Registration successful!");
                    LoginWindow loginWindow = new LoginWindow();
                    loginWindow.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Registration failed. Username might already exist.");
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("An error occurred: " + ex.Message);
            }
        }

        // Navigate back to the login window
        private void BackToLoginButton_Click(object sender, RoutedEventArgs e)
        {
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }
    }
}
