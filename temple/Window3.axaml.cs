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
using UserControlApp.ViewModels;

namespace UserControlApp;

public partial class Window3 : Window
{
    private CtlOnlyShowImage? _imageControl;

    public Window3()
    {
        // 窗口基础设置
        Title = "Window3 - 图像显示控件测试";
        Width = 1024;
        Height = 768;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        InitializeComponent();
        
        // 获取图像控件引用
        _imageControl = this.FindControl<CtlOnlyShowImage>("ImageControl");
        
        // 设置默认标定（示例：1像素 = 0.1mm）
        if (_imageControl != null)
        {
            var mmPerPixel = new Point(0.1, 0.1);
            _imageControl.SetCameraCalib(mmPerPixel, 1024, 768);
        }
    }

    /// <summary>
    /// 加载图像按钮点击事件
    /// </summary>
    private async void BtnLoadImage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择图像文件",
                Filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter { Name = "图像文件", Extensions = new List<string> { "png", "jpg", "jpeg", "bmp" } },
                    new FileDialogFilter { Name = "所有文件", Extensions = new List<string> { "*" } }
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
            // 简单的错误提示
            Console.WriteLine($"加载图像失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 添加图元按钮点击事件
    /// </summary>
    private void BtnAddElements_Click(object sender, RoutedEventArgs e)
    {
        if (_imageControl == null) return;

        var elements = new List<PaintElement>();

        // 添加一个红色圆（机械坐标：中心 (10, 20)，半径 5mm）
        elements.Add(new PaintElement
        {
            Type = PaintElementType.Circle,
            Pts = new List<double> { 10.0, 20.0, 25.0, 20.0 },
            Color = Colors.Red,
            LineWidth = 2.0,
            IsFill = false,
            Visible = true
        });

        // 添加一个绿色矩形
        elements.Add(new PaintElement
        {
            Type = PaintElementType.Rectangle,
            Pts = new List<double> { -20.0, -20.0, 20.0, 20.0 },
            Color = Colors.Green,
            LineWidth = 1.5,
            IsFill = false,
            Visible = true
        });

        // 添加一个蓝色十字（中心点）
        elements.Add(new PaintElement
        {
            Type = PaintElementType.Cross,
            Pts = new List<double> { 0.0, 0.0 },
            Color = Colors.Blue,
            LineWidth = 2.0,
            Visible = true
        });

        // 添加文本
        elements.Add(new PaintElement
        {
            Type = PaintElementType.Text,
            Pts = new List<double> { 0.0, -10.0 },  
            Text = "测试图元",
            FontSize = 16,
            Color = Colors.Yellow, 
            Visible = true
        });

        _imageControl.SetPaintElements(elements);
        _imageControl.CtlShowPaintStatus = ImageElementCtlStatus.ShowAll;
        _imageControl.ReFresh();
    }

    /// <summary>
    /// 清除图元按钮点击事件
    /// </summary>
    private void BtnClearElements_Click(object sender, RoutedEventArgs e)
    {
        if (_imageControl == null) return;

        _imageControl.SetPaintElements(new List<PaintElement>());
        _imageControl.ReFresh();
    }
}