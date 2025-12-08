# AvaloniaVisionControl
  

## 🚀 快速开始

### 安装

#### 方式1：NuGet 包

```bash
dotnet add package AvaloniaVisionControl
```

#### 方式2：项目引用

```xml
<ItemGroup>
  <ProjectReference Include="..\AvaloniaVisionControl\AvaloniaVisionControl.csproj" />
</ItemGroup>
```

#### 方式3：DLL 引用

```xml
<ItemGroup>
  <Reference Include="AvaloniaVisionControl">
    <HintPath>lib\AvaloniaVisionControl.dll</HintPath>
  </Reference>
</ItemGroup>
```

### 基本使用

#### 1. 在 XAML 中使用

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vision="using:AvaloniaVisionControl"
        x:Class="YourApp.MainWindow"
        Title="图像显示示例">
    
    <Grid>
        <vision:CtlOnlyShowImage x:Name="ImageControl"
                                 AllowMouseScroll="True"/>
    </Grid>
</Window>
```

#### 2. 在代码中使用

```csharp
using AvaloniaVisionControl;
using Avalonia.Media.Imaging;
using System.IO;

// 创建控件
var imageControl = new CtlOnlyShowImage(0);

// 设置标定（1像素 = 0.1mm）
imageControl.SetCameraCalib(new Point(0.1, 0.1), 1024, 768);

// 加载并显示图像
using var stream = File.OpenRead("image.png");
var bitmap = new Bitmap(stream);
var eventArgs = new ReceiveBitmapEventArgs(0, bitmap);
imageControl.ShowImage(eventArgs);
```

#### 3. 添加图元

```csharp
using System.Collections.Generic;
using Avalonia.Media;

var elements = new List<PaintElement>
{
    new PaintElement
    {
        Type = PaintElementType.Circle,
        Pts = new List<double> { 10.0, 20.0, 15.0, 20.0 },
        Color = Colors.Red,
        LineWidth = 2.0,
        Visible = true
    }
};

imageControl.SetPaintElements(elements);
imageControl.CtlShowPaintStatus = ImageElementCtlStatus.ShowAll;
imageControl.ReFresh();
```

## 📚 核心类

### CtlOnlyShowImage

主要的图像显示控件类。

**主要属性**：
- `AllowMouseScroll`: 是否允许鼠标滚轮缩放
- `NeedShowCam`: 需要显示的相机ID列表
- `CtlShowPaintStatus`: 图元显示状态
- `CtlMouseStatus`: 鼠标状态

**主要方法**：
- `ShowImage(ReceiveBitmapEventArgs)`: 显示图像
- `SetCameraCalib(...)`: 设置相机标定（多种重载）
- `SetPaintElements(List<PaintElement>)`: 设置图元列表
- `ChangePaintElement(int, PaintElement)`: 修改单个图元
- `ReFresh()`: 刷新显示
- `ConvertImageToMachinePosition(Point)`: 将图像像素坐标转换为机械坐标（绝对坐标，单位：mm）

**主要事件**：
- `ImageClick`: 鼠标左键单击事件，用于控制机械手移动

### PaintElement

图元类，用于定义要绘制的图形元素。

**支持的图元类型**：
- `Point`: 点
- `Line`: 线段
- `PolyLine`: 折线
- `Circle`: 圆
- `Rectangle`: 矩形
- `Ellipse`: 椭圆
- `Polygon`: 多边形
- `Cross`: 十字
- `Arrow`: 箭头
- `Ring`: 圆环
- `Arc`: 圆弧
- `Text`: 文本

### MotionMgr

运动管理器（单例），用于管理机械坐标。

```csharp
// 更新机械位置（单位：mm）
MotionMgr.Ins.UpdateMachPos(100.0, 200.0);

// 获取当前机械位置
var pos = MotionMgr.Ins.CurrMachPos;
```

## 🎨 使用辅助类

项目包含 `ImageControlHelper` 辅助类，提供更简洁的 API：

```csharp
using AvaloniaVisionControl;

// 快速创建控件
var imageControl = ImageControlHelper.CreateImageControl(
    cameraId: 0,
    mmPerPixel: new Point(0.1, 0.1),
    imageWidth: 1024,
    imageHeight: 768
);

// 快速创建图元
var circle = ImageControlHelper.CreateCircle(10, 20, 5, Colors.Red);
var line = ImageControlHelper.CreateLine(0, 0, 50, 50, Colors.Green);

// 批量添加图元
ImageControlHelper.AddPaintElements(imageControl, circle, line);
```

## 🖱️ 鼠标交互

控件默认支持以下鼠标操作：

- **滚轮缩放**：鼠标滚轮上下滚动，以鼠标位置为中心缩放
- **拖拽平移**：按住鼠标左键拖动图像
- **双击复位**：双击图像恢复到默认缩放比例
- **左键单击**：在图像上单击鼠标左键，触发 `ImageClick` 事件（用于控制机械手移动）

禁用滚轮缩放：
```csharp
imageControl.AllowMouseScroll = false;
```

### 使用鼠标单击控制机械手

控件提供了 `ImageClick` 事件，当用户在图像上单击鼠标左键时触发。事件参数包含：
- `ControlPosition`：鼠标在控件中的位置（控件坐标）
- `ImagePosition`：鼠标在图像中的位置（图像原始像素坐标）

**基本使用**：

```csharp
// 订阅单击事件
imageControl.ImageClick += (sender, e) =>
{
    // e.ImagePosition 是图像中的像素坐标
    Console.WriteLine($"点击位置 - 图像坐标: X={e.ImagePosition.X:F2}, Y={e.ImagePosition.Y:F2}");
    
    // 将图像坐标转换为机械坐标（绝对坐标，单位：mm）
    Point machinePos = imageControl.ConvertImageToMachinePosition(e.ImagePosition);
    Console.WriteLine($"机械坐标: X={machinePos.X:F2}mm, Y={machinePos.Y:F2}mm");
    
    // 控制机械手移动到该位置
    // MoveRobotTo(machinePos.X, machinePos.Y);
};
```

**完整示例：点击图像控制机械手移动**：

```csharp
using Avalonia;
using AvaloniaVisionControl;

public class RobotControlExample
{
    private CtlOnlyShowImage _imageControl;
    
    public void Initialize()
    {
        // 1. 创建图像控件
        _imageControl = new CtlOnlyShowImage(0);
        
        // 2. 设置相机标定（必须设置，才能正确转换坐标）
        // 假设：1像素 = 0.1mm，图像尺寸 1024x768
        _imageControl.SetCameraCalib(new Point(0.1, 0.1), 1024, 768);
        
        // 3. 设置当前机械位置（视野中心对应的机械坐标）
        MotionMgr.Ins.UpdateMachPos(100.0, 200.0); // 单位：mm
        
        // 4. 订阅单击事件
        _imageControl.ImageClick += OnImageClick;
    }
    
    private void OnImageClick(object sender, ImageClickEventArgs e)
    {
        // 获取图像像素坐标
        Point imagePos = e.ImagePosition;
        
        // 转换为机械坐标（绝对坐标）
        Point machinePos = _imageControl.ConvertImageToMachinePosition(imagePos);
        
        // 控制机械手移动到目标位置
        MoveRobotToPosition(machinePos.X, machinePos.Y);
    }
    
    private void MoveRobotToPosition(double x, double y)
    {
        // 这里实现您的机械手控制逻辑
        // 例如：调用机械手控制 API
        Console.WriteLine($"移动机械手到: X={x:F2}mm, Y={y:F2}mm");
        
        // 示例：更新机械位置（如果机械手移动成功）
        // MotionMgr.Ins.UpdateMachPos(x, y);
    }
}
```

**注意事项**：

1. **必须设置相机标定**：在使用坐标转换功能前，必须先调用 `SetCameraCalib` 方法设置标定参数
2. **更新机械位置**：当机械手实际移动后，应调用 `MotionMgr.Ins.UpdateMachPos` 更新当前机械位置，以便图元正确显示
3. **坐标系统**：
   - 图像坐标原点在左上角（像素坐标）
   - 机械坐标原点由标定确定，通常视野中心对应当前机械位置
   - `ConvertImageToMachinePosition` 返回的是绝对机械坐标（单位：mm）
4. **单击与拖拽**：系统会自动区分单击和拖拽操作，只有真正的单击（移动距离 < 5像素）才会触发事件

## ⚙️ 相机标定

### 方式1：简化标定（像素当量）

```csharp
// 设置像素当量：1像素 = 0.1mm
var mmPerPixel = new Point(0.1, 0.1);
imageControl.SetCameraCalib(mmPerPixel, imageWidth, imageHeight);
```

### 方式2：变换矩阵

```csharp
// 像素到机械坐标的变换矩阵（9元素数组）
double[] matrixPixToMM = new double[9] { /* ... */ };
imageControl.SetCameraCalib(matrixPixToMM);

// 或机械坐标到像素的变换矩阵
double[] matrixMMToPix = new double[9] { /* ... */ };
imageControl.SetCameraCalibRef(matrixMMToPix);
```

## 📋 完整示例

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using AvaloniaVisionControl;
using System.Collections.Generic;
using System.IO;

public class ImageViewerExample
{
    private CtlOnlyShowImage _imageControl;
    
    public void Initialize()
    {
        // 1. 创建控件
        _imageControl = new CtlOnlyShowImage(0);
        
        // 2. 设置标定
        _imageControl.SetCameraCalib(new Point(0.1, 0.1), 1024, 768);
        
        // 3. 加载图像
        LoadImage("test.png");
        
        // 4. 添加图元
        AddPaintElements();
        
        // 5. 更新机械位置
        MotionMgr.Ins.UpdateMachPos(100.0, 200.0);
    }
    
    private void LoadImage(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var bitmap = new Bitmap(stream);
        var eventArgs = new ReceiveBitmapEventArgs(0, bitmap);
        _imageControl.ShowImage(eventArgs);
    }
    
    private void AddPaintElements()
    {
        var elements = new List<PaintElement>
        {
            new PaintElement
            {
                Type = PaintElementType.Circle,
                Pts = new List<double> { 10.0, 20.0, 15.0, 20.0 },
                Color = Colors.Red,
                LineWidth = 2.0,
                Visible = true
            },
            new PaintElement
            {
                Type = PaintElementType.Cross,
                Pts = new List<double> { 0.0, 0.0 },
                Color = Colors.Blue,
                LineWidth = 2.0,
                Visible = true
            }
        };
        
        _imageControl.SetPaintElements(elements);
        _imageControl.CtlShowPaintStatus = ImageElementCtlStatus.ShowAll;
        _imageControl.ReFresh();
    }
}
```

## ⚠️ 注意事项

1. **线程安全**：图像更新会自动在 UI 线程执行
2. **资源释放**：控件会在从视觉树分离时自动释放图像资源
3. **坐标系统**：
   - 图元坐标使用**机械坐标**（单位：mm）
   - 原点为视野中心
   - 通过 `MotionMgr.Ins.CurrMachPos` 设置当前机械位置
4. **性能建议**：
   - 建议图元数量 < 1000 个
   - 大图像建议使用合适的缩放比例

## 📦 生成 NuGet 包

```bash
dotnet pack -c Release
```

生成的包位于：`bin/Release/AvaloniaVisionControl.1.0.0.nupkg`


