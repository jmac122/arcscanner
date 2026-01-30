using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ArcRaidersOverlay;

public partial class CalibrationWindow : Window
{
    private System.Windows.Point _startPoint;
    private bool _isDrawing;
    private readonly ScreenCapture _screenCapture;

    public RegionConfig SelectedRegion { get; private set; } = new();

    public CalibrationWindow(string regionName)
    {
        InitializeComponent();

        _screenCapture = new ScreenCapture();

        InstructionText.Text = $"Draw a rectangle to select the {regionName}";

        Loaded += OnLoaded;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        KeyDown += OnKeyDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Capture the entire screen
        var screenWidth = (int)SystemParameters.VirtualScreenWidth;
        var screenHeight = (int)SystemParameters.VirtualScreenHeight;
        var screenLeft = (int)SystemParameters.VirtualScreenLeft;
        var screenTop = (int)SystemParameters.VirtualScreenTop;

        using var bitmap = _screenCapture.CaptureRegion(new Rectangle(
            screenLeft, screenTop, screenWidth, screenHeight));

        // Convert to BitmapSource for WPF
        var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
            bitmap.GetHbitmap(),
            IntPtr.Zero,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());

        ScreenshotImage.Source = bitmapSource;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(this);
        _isDrawing = true;

        SelectionRect.Visibility = Visibility.Visible;
        SelectionRect.Width = 0;
        SelectionRect.Height = 0;
        SelectionRect.Margin = new Thickness(_startPoint.X, _startPoint.Y, 0, 0);

        CoordinatesPanel.Visibility = Visibility.Visible;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDrawing) return;

        var currentPoint = e.GetPosition(this);

        var x = Math.Min(_startPoint.X, currentPoint.X);
        var y = Math.Min(_startPoint.Y, currentPoint.Y);
        var width = Math.Abs(currentPoint.X - _startPoint.X);
        var height = Math.Abs(currentPoint.Y - _startPoint.Y);

        SelectionRect.Margin = new Thickness(x, y, 0, 0);
        SelectionRect.Width = width;
        SelectionRect.Height = height;

        CoordinatesText.Text = $"X: {(int)x}  Y: {(int)y}\nW: {(int)width}  H: {(int)height}";
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDrawing) return;
        _isDrawing = false;

        var currentPoint = e.GetPosition(this);

        var x = (int)Math.Min(_startPoint.X, currentPoint.X);
        var y = (int)Math.Min(_startPoint.Y, currentPoint.Y);
        var width = (int)Math.Abs(currentPoint.X - _startPoint.X);
        var height = (int)Math.Abs(currentPoint.Y - _startPoint.Y);

        if (width < 10 || height < 10)
        {
            MessageBox.Show("Selection too small. Please draw a larger rectangle.",
                "Invalid Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SelectedRegion = new RegionConfig
        {
            X = x,
            Y = y,
            Width = width,
            Height = height
        };

        DialogResult = true;
        Close();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }
}
