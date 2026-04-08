using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvaloniaVisionControl;
using System;
using System.Collections.Generic;
using System.IO;

namespace UserControlApp;

public partial class Window3 : Window
{
    private CtlOnlyShowImage? _imageControl;
    private TextBox? _logTextBox;

    public Window3()
    {
        Title = "Window3 - Interaction Verification";
        Width = 1200;
        Height = 820;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        InitializeComponent();

        _imageControl = this.FindControl<CtlOnlyShowImage>("ImageControl");
        _logTextBox = this.FindControl<TextBox>("TxtLog");

        if (_imageControl != null)
        {
            _imageControl.SetCameraCalib(new Point(0.1, 0.1), 1024, 768);
            _imageControl.CtlShowPaintStatus = ImageElementCtlStatus.ShowAll;
            _imageControl.ImageClick += OnImageClick;
        }

        Log("Ready. 1) Load an image 2) Load verification shapes 3) Validate resize/move/pan behavior.");
        LoadVerificationShapes();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_imageControl != null)
        {
            _imageControl.ImageClick -= OnImageClick;
        }

        base.OnClosed(e);
    }

    private void OnImageClick(object? sender, ImageClickEventArgs e)
    {
        Log($"ImageClick at image pixel ({e.ImagePosition.X:F1}, {e.ImagePosition.Y:F1}).");
    }

    private async void BtnLoadImage_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select image file",
                Filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter { Name = "Image Files", Extensions = new List<string> { "png", "jpg", "jpeg", "bmp" } },
                    new FileDialogFilter { Name = "All Files", Extensions = new List<string> { "*" } }
                }
            };

            var result = await dialog.ShowAsync(this);
            if (result == null || result.Length == 0)
            {
                Log("Load image canceled.");
                return;
            }

            var filePath = result[0];
            using var stream = File.OpenRead(filePath);
            var bitmap = new Bitmap(stream);

            if (_imageControl != null)
            {
                _imageControl.ShowImage(new ReceiveBitmapEventArgs(0, bitmap));
                _imageControl.ReFresh();
            }

            Log($"Image loaded: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            Log($"Load image failed: {ex.Message}");
        }
    }

    private void BtnAddElements_Click(object? sender, RoutedEventArgs e)
    {
        LoadVerificationShapes();
    }

    private void BtnClearElements_Click(object? sender, RoutedEventArgs e)
    {
        if (_imageControl == null)
        {
            return;
        }

        _imageControl.SetPaintElements(new List<PaintElement>());
        _imageControl.ReFresh();
        Log("Shapes cleared.");
    }

    private void LoadVerificationShapes()
    {
        if (_imageControl == null)
        {
            return;
        }

        var elements = new List<PaintElement>
        {
            new PaintElement
            {
                Type = PaintElementType.Rectangle,
                Pts = new List<double> { -30.0, -15.0, -5.0, 10.0 },
                Color = Colors.Lime,
                LineWidth = 2.0,
                IsFill = false,
                Visible = true
            },
            new PaintElement
            {
                Type = PaintElementType.Circle,
                Pts = new List<double> { 15.0, 8.0, 22.0, 8.0 },
                Color = Colors.OrangeRed,
                LineWidth = 2.0,
                IsFill = false,
                Visible = true
            }
        };

        _imageControl.SetPaintElements(elements);
        _imageControl.CtlShowPaintStatus = ImageElementCtlStatus.ShowAll;
        _imageControl.ReFresh();

        Log("Verification shapes loaded (1 rectangle + 1 circle).");
        Log("Try rectangle: 8 handles resize, body move; circle: center handle move, edge handle resize.");
        Log("ImageClick should appear only when clicking blank area.");
    }

    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";

        if (_logTextBox == null)
        {
            Console.WriteLine(line);
            return;
        }

        if (string.IsNullOrWhiteSpace(_logTextBox.Text))
        {
            _logTextBox.Text = line;
        }
        else
        {
            _logTextBox.Text += Environment.NewLine + line;
        }

        _logTextBox.CaretIndex = _logTextBox.Text?.Length ?? 0;
    }
}
