using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Templates;
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

    public Window3()
    {
        // ���ڻ�������
        Title = "Window3 - ͼ����ʾ�ؼ�����";
        Width = 1024;
        Height = 768;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        InitializeComponent();
        
        // ��ȡͼ��ؼ�����
        _imageControl = this.FindControl<CtlOnlyShowImage>("ImageControl");
        
        // 设置默认标定（假设1像素 = 0.1mm）
        if (_imageControl != null)
        {
            var mmPerPixel = new Point(0.1, 0.1);
            // 默认 xRever = -1, yRever = -1
            _imageControl.SetCameraCalib(mmPerPixel, 1024, 768, -1, -1);
        }
    }

    /// <summary>
    /// ����ͼ��ť����¼�
    /// </summary>
    private async void BtnLoadImage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "ѡ��ͼ���ļ�",
                Filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter { Name = "ͼ���ļ�", Extensions = new List<string> { "png", "jpg", "jpeg", "bmp" } },
                    new FileDialogFilter { Name = "�����ļ�", Extensions = new List<string> { "*" } }
                }
            };

            var result = await dialog.ShowAsync(this);
            if (result != null && result.Length > 0)
            {
                var filePath = result[0];
                using var stream = File.OpenRead(filePath);
                var bitmap = new Bitmap(stream);
                
                if (_imageControl != null)
                {
                    var eventArgs = new ReceiveBitmapEventArgs(0, bitmap);
                    _imageControl.ShowImage(eventArgs);
                }
            }
        }
        catch (Exception ex)
        {
            // �򵥵Ĵ�����ʾ
            Console.WriteLine($"����ͼ��ʧ��: {ex.Message}");
        }
    }

    /// <summary>
    /// ����ͼԪ��ť����¼�
    /// </summary>
    private void BtnAddElements_Click(object sender, RoutedEventArgs e)
    {
        if (_imageControl == null) return;

        var elements = new List<PaintElement>();

        // ����һ����ɫԲ����е���꣺���� (10, 20)���뾶 5mm��
        elements.Add(new PaintElement
        {
            Type = PaintElementType.Circle,
            Pts = new List<double> { 10.0, 20.0, 25.0, 20.0 },
            Color = Colors.Red,
            LineWidth = 2.0,
            IsFill = false,
            Visible = true
        });

        // ����һ����ɫ����
        elements.Add(new PaintElement
        {
            Type = PaintElementType.Rect,
            Pts = new List<double> { -20.0, -20.0, 20.0, 20.0 },
            Color = Colors.Green,
            LineWidth = 1.5,
            IsFill = false,
            Visible = true
        });

        // ����һ����ɫʮ�֣����ĵ㣩
        elements.Add(new PaintElement
        {
            Type = PaintElementType.Cross,
            Pts = new List<double> { 0.0, 0.0 },
            Color = Colors.Blue,
            LineWidth = 2.0,
            Visible = true
        });

        // �����ı�
        elements.Add(new PaintElement
        {
            Type = PaintElementType.Text,
            Pts = new List<double> { 0.0, -10.0 },  
            Text = "����ͼԪ",
            FontSize = 16,
            Color = Colors.Yellow, 
            Visible = true
        });

        _imageControl.SetPaintElements(elements);
        _imageControl.CtlShowPaintStatus = ImageElementCtlStatus.ShowDragImageAndLayer;
        _imageControl.ReFresh();
    }

    /// <summary>
    /// ���ͼԪ��ť����¼�
    /// </summary>
    private void BtnClearElements_Click(object sender, RoutedEventArgs e)
    {
        if (_imageControl == null) return;

        _imageControl.SetPaintElements(new List<PaintElement>());
        _imageControl.ReFresh();
    }
}