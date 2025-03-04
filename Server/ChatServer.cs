using System.Net;
using System.Net.Sockets;
using System.Text;
using Server.Database;

namespace Server
{
    public class ChatServer
    {
        private TcpListener _listener;
        private bool _isRunning;
        private List<ClientHandler> _connectedClients = new List<ClientHandler>();

        /// <summary>
        /// Starts the chat server on a given IP and port.
        /// </summary>
        public void StartServer(string ip, int port)
        {
            _listener = new TcpListener(IPAddress.Parse(ip), port);
            _listener.Start();
            _isRunning = true;
            Console.WriteLine($"Server started on {ip}:{port}");

            // Accept clients in a background thread
            Task.Run(() => AcceptClients());
        }

        /// <summary>
        /// Accepts incoming client connections.
        /// </summary>
        private async Task AcceptClients()
        {
            while (_isRunning)
            {
                TcpClient tcpClient = await _listener.AcceptTcpClientAsync();
                ClientHandler handler = new ClientHandler(tcpClient, this);
                lock (_connectedClients)
                {
                    _connectedClients.Add(handler);
                }

                // Handle client in a new task
                Task.Run(() => handler.HandleClientAsync());
            }
        }

        /// <summary>
        /// Broadcasts a message to all connected clients or a specific subset.
        /// This can be adapted to send to a single client or a group.
        /// </summary>
        public void BroadcastMessage(string message)
        {
            lock (_connectedClients)
            {
                foreach (var client in _connectedClients)
                {
                    client.SendMessage(message);
                }
            }
        }

        /// <summary>
        /// Removes a client from the server when the connection is closed.
        /// </summary>
        /// <param name="client">ClientHandler to remove.</param>
        public void RemoveClient(ClientHandler client)
        {
            lock (_connectedClients)
            {
                if (_connectedClients.Contains(client))
                {
                    _connectedClients.Remove(client);
                }
            }
        }
    }

    /// <summary>
    /// Represents a single connected client and its communication.
    /// </summary>
    public class ClientHandler
    {
        private TcpClient _client;
        private ChatServer _server;
        private NetworkStream _stream;
        private string _username;
        private bool _isAuthenticated = false; // Track if client is logged in

        public ClientHandler(TcpClient client, ChatServer server)
        {
            _client = client;
            _server = server;
        }

        /// <summary>
        /// Main loop that handles receiving data from this client.
        /// </summary>
        public async Task HandleClientAsync()
        {
            _stream = _client.GetStream();
            byte[] buffer = new byte[1024];
            int bytesRead;

            // Send welcome and authentication instructions
            await _stream.WriteAsync(Encoding.UTF8.GetBytes("Welcome to the chat server!\n" +
                                                            "Please login with: /login username password\n" +
                                                            "Or register with: /register username password\n"));

            try
            {
                while ((bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                    Console.WriteLine($"Received: {message} from {_client.Client.RemoteEndPoint}");

                    // Process commands for authentication
                    if (message.StartsWith("/login"))
                    {
                        // Expected format: /login username password
                        string[] parts = message.Split(' ', 3);
                        if (parts.Length < 3)
                        {
                            await _stream.WriteAsync(
                                Encoding.UTF8.GetBytes("Invalid login command. Use: /login username password\n"));
                            continue;
                        }

                        string username = parts[1];
                        string password = parts[2];

                        if (await AuthHelper.LoginUserAsync(username, password))
                        {
                            _isAuthenticated = true;
                            _username = username;
                            await _stream.WriteAsync(Encoding.UTF8.GetBytes("Login successful!\n"));
                        }
                        else
                        {
                            await _stream.WriteAsync(Encoding.UTF8.GetBytes("Login failed. Check your credentials.\n"));
                        }

                        continue;
                    }
                    else if (message.StartsWith("/register"))
                    {
                        // Expected format: /register username password
                        string[] parts = message.Split(' ', 3);
                        if (parts.Length < 3)
                        {
                            await _stream.WriteAsync(
                                Encoding.UTF8.GetBytes("Invalid register command. Use: /register username password\n"));
                            continue;
                        }

                        string username = parts[1];
                        string password = parts[2];

                        if (await AuthHelper.RegisterUserAsync(username, password))
                        {
                            await _stream.WriteAsync(
                                Encoding.UTF8.GetBytes("Registration successful! Please login using /login.\n"));
                        }
                        else
                        {
                            await _stream.WriteAsync(
                                Encoding.UTF8.GetBytes("Registration failed. Username might already exist.\n"));
                        }

                        continue;
                    }

                    // If not authenticated, do not process regular messages
                    if (!_isAuthenticated)
                    {
                        await _stream.WriteAsync(Encoding.UTF8.GetBytes("You must be logged in to send messages.\n"));
                        continue;
                    }

                    // Process chat messages (prepend username)
                    string fullMessage = $"{_username}: {message}";
                    _server.BroadcastMessage(fullMessage);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                _stream.Close();
                _client.Close();
                _server.RemoveClient(this);
            }
        }

        /// <summary>
        /// Sends a message to this client.
        /// </summary>
        public void SendMessage(string message)
        {
            if (_stream != null)
            {
                byte[] data = Encoding.UTF8.GetBytes(message + "\n");
                _stream.WriteAsync(data, 0, data.Length);
            }
        }
    }
}