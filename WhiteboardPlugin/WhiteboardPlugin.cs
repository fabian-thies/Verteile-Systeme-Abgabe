using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

public class WhiteboardPlugin : IPlugin
{
    public string Name => "Whiteboard";

    private WhiteboardWindow _whiteboardWindow;

    public void Initialize()
    {
        _whiteboardWindow = new WhiteboardWindow();
    }

    public void Execute()
    {
        if (_whiteboardWindow == null)
        {
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

    public WhiteboardWindow()
    {
        Title = "Whiteboard";
        Width = 800;
        Height = 600;
        Background = Brushes.White;

        _canvas = new Canvas();
        Content = _canvas;

        _canvas.MouseDown += Canvas_MouseDown;
        _canvas.MouseMove += Canvas_MouseMove;
        _canvas.MouseUp += Canvas_MouseUp;
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
            Line line = new Line
            {
                Stroke = Brushes.Black,
                StrokeThickness = 2,
                X1 = _previousPoint.X,
                Y1 = _previousPoint.Y,
                X2 = currentPoint.X,
                Y2 = currentPoint.Y
            };
            _canvas.Children.Add(line);
            _previousPoint = currentPoint;
        }
    }

    private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDrawing = false;
    }
}