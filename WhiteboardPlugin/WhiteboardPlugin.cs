// WhiteboardPlugin.cs

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.AspNetCore.SignalR.Client;

public class WhiteboardPlugin : IPlugin
{
    public string Name => "Whiteboard";

    private WhiteboardWindow _whiteboardWindow;

    // Overloaded Initialize method for collaborative drawing without specific target (broadcast)
    public void Initialize(HubConnection connection)
    {
        _whiteboardWindow = new WhiteboardWindow(connection);
    }

    // Overloaded Initialize method to accept a SignalR connection, a target identifier and mode (true = group, false = private)
    public void Initialize(HubConnection connection, string target, bool isGroup)
    {
        _whiteboardWindow = new WhiteboardWindow(connection, target, isGroup);
    }

    // Default Initialize method for non-collaborative mode.
    public void Initialize()
    {
        _whiteboardWindow = new WhiteboardWindow(null);
    }

    public void Execute()
    {
        if (_whiteboardWindow == null)
        {
            // Falls keine Verbindung oder kein Ziel übergeben wurde, nutze den Broadcast-Modus.
            Initialize();
        }

        _whiteboardWindow.Show();
        _whiteboardWindow.Activate();
    }
}

public class WhiteboardWindow : Window
{
    private bool _isDrawing;
    private Point _previousPoint;
    private readonly Canvas _canvas;
    private readonly HubConnection _connection;
    private readonly string _target;
    private readonly bool _isGroupMode;

    // Constructor for non-collaborative or broadcast mode.
    public WhiteboardWindow(HubConnection connection)
        : this(connection, null, false)
    {
    }

    // Constructor for collaborative mode with target identifier.
    public WhiteboardWindow(HubConnection connection, string target, bool isGroup)
    {
        _connection = connection;
        _target = target;
        _isGroupMode = isGroup;

        Title = "Whiteboard";
        Width = 800;
        Height = 600;
        Background = Brushes.White;

        _canvas = new Canvas
        {
            Focusable = true,
            Background = Brushes.Transparent
        };
        Content = _canvas;

        // Subscribe to mouse events for drawing.
        _canvas.MouseDown += Canvas_MouseDown;
        _canvas.MouseMove += Canvas_MouseMove;
        _canvas.MouseUp += Canvas_MouseUp;

        this.Loaded += WhiteboardWindow_Loaded;

        // Register to receive whiteboard drawing updates from the server if a connection exists.
        if (_connection != null)
        {
            _connection.On<double, double, double, double>("ReceiveWhiteboardLine",
                (x1, y1, x2, y2) => { Dispatcher.Invoke(() => { DrawLine(x1, y1, x2, y2); }); });
        }
    }

    private void WhiteboardWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _canvas.Focus();
        Keyboard.Focus(_canvas);
    }

    private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _isDrawing = true;
            _previousPoint = e.GetPosition(_canvas);
        }
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDrawing && e.LeftButton == MouseButtonState.Pressed)
        {
            Point currentPoint = e.GetPosition(_canvas);

            // Draw the line locally.
            DrawLine(_previousPoint.X, _previousPoint.Y, currentPoint.X, currentPoint.Y);

            // Send drawing data to server if a connection exists.
            if (_connection != null)
            {
                if (!string.IsNullOrEmpty(_target))
                {
                    // Send drawing data to a specific target (group or private).
                    _connection.InvokeAsync("SendWhiteboardLine", _target, _isGroupMode, _previousPoint.X,
                        _previousPoint.Y, currentPoint.X, currentPoint.Y);
                }
                else
                {
                    // Broadcast to all clients using the renamed method.
                    _connection.InvokeAsync("SendWhiteboardLineBroadcast", _previousPoint.X, _previousPoint.Y,
                        currentPoint.X, currentPoint.Y);
                }
            }

            _previousPoint = currentPoint;
        }
    }

    private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDrawing = false;
    }

    // Draw a line on the canvas.
    private void DrawLine(double x1, double y1, double x2, double y2)
    {
        Line line = new Line
        {
            Stroke = Brushes.Black,
            StrokeThickness = 2,
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2
        };
        _canvas.Children.Add(line);
    }
}