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

    // Overloaded Initialize method to accept a SignalR connection for collaborative drawing.
    public void Initialize(HubConnection connection)
    {
        // Initialize the whiteboard window with the provided SignalR connection.
        _whiteboardWindow = new WhiteboardWindow(connection);
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
            // For collaborative mode, pass the existing HubConnection if available.
            // Otherwise, default to non-collaborative mode.
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

    // Constructor accepting an optional SignalR connection.
    public WhiteboardWindow(HubConnection connection)
    {
        _connection = connection;
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
            _connection.On<double, double, double, double>("ReceiveWhiteboardLine", (x1, y1, x2, y2) =>
            {
                Dispatcher.Invoke(() =>
                {
                    DrawLine(x1, y1, x2, y2);
                });
            });
        }
    }

    private void WhiteboardWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Set focus to the canvas to capture input events.
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

            // Draw the line locally on the canvas.
            DrawLine(_previousPoint.X, _previousPoint.Y, currentPoint.X, currentPoint.Y);

            // Send the drawing data to the server for collaborative drawing.
            if (_connection != null)
            {
                _connection.InvokeAsync("SendWhiteboardLine", _previousPoint.X, _previousPoint.Y, currentPoint.X, currentPoint.Y);
            }

            _previousPoint = currentPoint;
        }
    }

    private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDrawing = false;
    }

    // Draws a line on the canvas.
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
