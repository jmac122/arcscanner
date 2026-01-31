using System.Windows.Shapes;
using Rectangle = System.Drawing.Rectangle;

namespace ArcRaidersOverlay;

public partial class CalibrationWindow : Window
{
    private System.Windows.Point _startPoint;
    private bool _isDrawing;
    private readonly ScreenCapture _screenCapture;
    private readonly GameWindowDetector _gameDetector;
    private readonly bool _useGameRelative;
    private GameWindowInfo? _gameWindow;

    // Screen offset for virtual screen coordinates
    private int _screenLeft;
    private int _screenTop;

    /// <summary>
    /// The selected region. If UseGameRelativeCoordinates is true,
    /// this will be relative to the game window.
    /// </summary>
    public RegionConfig SelectedRegion { get; private set; } = new();

    public CalibrationWindow(string regionName, bool useGameRelative = true)
    {
        InitializeComponent();

        _screenCapture = new ScreenCapture();
        _gameDetector = new GameWindowDetector();
        _useGameRelative = useGameRelative;

        InstructionText.Text = $"Draw a rectangle to select the {regionName}";

        Loaded += OnLoaded;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        KeyDown += OnKeyDown;
    }

    protected override void OnClosed(EventArgs e)
    {
        _gameDetector.Dispose();
        base.OnClosed(e);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Get virtual screen bounds (covers all monitors)
        var screenWidth = (int)SystemParameters.VirtualScreenWidth;
        var screenHeight = (int)SystemParameters.VirtualScreenHeight;
        _screenLeft = (int)SystemParameters.VirtualScreenLeft;
        _screenTop = (int)SystemParameters.VirtualScreenTop;

        // Capture the entire virtual screen
        using var bitmap = _screenCapture.CaptureRegion(new Rectangle(
            _screenLeft, _screenTop, screenWidth, screenHeight));

        // Convert to BitmapSource for WPF
        var hBitmap = bitmap.GetHbitmap();
        try
        {
            var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            ScreenshotImage.Source = bitmapSource;
        }
        finally
        {
            // Free the HBitmap to prevent memory leak
            DeleteObject(hBitmap);
        }

        // Detect game window and draw overlay
        _gameDetector.DetectGameWindow();
        _gameWindow = _gameDetector.CurrentWindow;

        if (_gameWindow != null)
        {
            DrawGameWindowOverlay();
            UpdateInstructionText();
        }
        else if (_useGameRelative)
        {
            InstructionText.Text += "\n(Game not detected - using absolute coordinates)";
        }
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    private void DrawGameWindowOverlay()
    {
        if (_gameWindow == null) return;

        // Convert game bounds to window-relative coordinates
        var left = _gameWindow.Bounds.X - _screenLeft;
        var top = _gameWindow.Bounds.Y - _screenTop;

        // Create a rectangle showing the game window bounds
        var gameRect = new System.Windows.Shapes.Rectangle
        {
            Width = _gameWindow.Bounds.Width,
            Height = _gameWindow.Bounds.Height,
            Stroke = new SolidColorBrush(Colors.Lime),
            StrokeThickness = 3,
            StrokeDashArray = new DoubleCollection { 5, 5 },
            Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 0, 255, 0)),
            IsHitTestVisible = false
        };

        // Position the rectangle
        System.Windows.Controls.Canvas.SetLeft(gameRect, left);
        System.Windows.Controls.Canvas.SetTop(gameRect, top);

        // Add to the overlay canvas
        if (OverlayCanvas != null)
        {
            OverlayCanvas.Children.Add(gameRect);

            // Add label for game window
            var label = new System.Windows.Controls.TextBlock
            {
                Text = $"ARC Raiders ({_gameWindow.Bounds.Width}x{_gameWindow.Bounds.Height})",
                Foreground = new SolidColorBrush(Colors.Lime),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 0, 0, 0)),
                Padding = new Thickness(5, 2, 5, 2),
                IsHitTestVisible = false
            };

            System.Windows.Controls.Canvas.SetLeft(label, left + 5);
            System.Windows.Controls.Canvas.SetTop(label, top + 5);
            OverlayCanvas.Children.Add(label);
        }
    }

    private void UpdateInstructionText()
    {
        if (_gameWindow != null && _useGameRelative)
        {
            InstructionText.Text += $"\nGame detected at ({_gameWindow.Bounds.X}, {_gameWindow.Bounds.Y}) - " +
                                    "coordinates will be saved relative to game window";
        }
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

        // Show both screen and game-relative coordinates
        var screenX = (int)x + _screenLeft;
        var screenY = (int)y + _screenTop;

        if (_gameWindow != null && _useGameRelative)
        {
            var relX = screenX - _gameWindow.Bounds.X;
            var relY = screenY - _gameWindow.Bounds.Y;
            CoordinatesText.Text = $"Screen: X={screenX} Y={screenY}\n" +
                                   $"Game: X={relX} Y={relY}\n" +
                                   $"Size: {(int)width} x {(int)height}";
        }
        else
        {
            CoordinatesText.Text = $"X: {screenX}  Y: {screenY}\nW: {(int)width}  H: {(int)height}";
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDrawing) return;
        _isDrawing = false;

        var currentPoint = e.GetPosition(this);

        // Calculate in window coordinates first
        var windowX = (int)Math.Min(_startPoint.X, currentPoint.X);
        var windowY = (int)Math.Min(_startPoint.Y, currentPoint.Y);
        var width = (int)Math.Abs(currentPoint.X - _startPoint.X);
        var height = (int)Math.Abs(currentPoint.Y - _startPoint.Y);

        if (width < 10 || height < 10)
        {
            MessageBox.Show("Selection too small. Please draw a larger rectangle.",
                "Invalid Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Convert to screen coordinates
        var screenX = windowX + _screenLeft;
        var screenY = windowY + _screenTop;

        // Store screen region
        var selectedScreenRegion = new RegionConfig
        {
            X = screenX,
            Y = screenY,
            Width = width,
            Height = height
        };

        // Calculate game-relative if applicable
        if (_gameWindow != null && _useGameRelative)
        {
            SelectedRegion = new RegionConfig
            {
                X = screenX - _gameWindow.Bounds.X,
                Y = screenY - _gameWindow.Bounds.Y,
                Width = width,
                Height = height
            };

            System.Diagnostics.Debug.WriteLine(
                $"Calibrated region: Screen({screenX},{screenY}) -> Game({SelectedRegion.X},{SelectedRegion.Y}) " +
                $"Size: {width}x{height}");
        }
        else
        {
            // Use absolute screen coordinates
            SelectedRegion = selectedScreenRegion;
        }

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
