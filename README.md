# AvaloniaVisionControl

`AvaloniaVisionControl` 是一个面向 Avalonia 的图像显示控件，提供：

- 图像显示（棋盘背景）
- 鼠标滚轮缩放、左键拖拽平移、双击复位
- 图元叠加绘制（点/线/圆/矩形/椭圆/多边形/文本/箭头等）
- 图元交互编辑（选中、拖动、句柄缩放）
- 视口自适应：窗口 `resize` 时自动重算默认缩放并约束平移边界

当前版本使用**纯图像像素坐标模式**。

快速入口：

- 使用文档（面向 UserControlApp/二次开发）：[USAGE.md](./USAGE.md)

## 1. 安装

### NuGet

```bash
dotnet add package AvaloniaVisionControl
```

### 项目引用

```xml
<ItemGroup>
  <ProjectReference Include="..\AvaloniaVisionControl\AvaloniaVisionControl.csproj" />
</ItemGroup>
```

## 2. 快速接入

### XAML

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vision="using:AvaloniaVisionControl"
        x:Class="YourApp.MainWindow">

  <vision:CtlOnlyShowImage x:Name="ImageControl"/>
</Window>
```

### C#（推荐）

```csharp
using AvaloniaVisionControl;

// 只接收 cameraId=0 的图像
var imageControl = new CtlOnlyShowImage(0)
{
    AllowMouseScroll = true
};

// 图元默认不显示，需要显式设置
imageControl.CtlShowPaintStatus = ImageElementCtlStatus.ShowAll;
```

### 属性绑定（Avalonia Property）

控件的以下公开属性已使用 Avalonia Property，可直接参与 XAML 绑定/样式：

- `AllowMouseScroll`
- `NeedShowCam`
- `CtlShowPaintStatus`
- `CtlMouseStatus`

示例：

```xml
<vision:CtlOnlyShowImage
    AllowMouseScroll="{Binding EnableWheelZoom}"
    NeedShowCam="{Binding ActiveCameraIds}"
    CtlShowPaintStatus="{Binding PaintDisplayMode}" />
```

## 3. 图像输入（重点）

控件支持 3 种输入方式：

### 3.1 推荐：`ShowImageFromStream`

```csharp
using var stream = File.OpenRead("test.png");
int code = imageControl.ShowImageFromStream(0, stream);
```

- `0`：成功
- `-1`：相机 ID 不匹配
- `-2`：参数或图像数据无效

### 3.2 推荐：`ShowImageCopy`

```csharp
int code = imageControl.ShowImageCopy(0, bitmap);
```

适合你仍要继续使用 `bitmap` 的场景。

说明：`ShowImageCopy` 当前使用像素复制路径（`CopyPixels`），不再走 `Save -> Stream -> Decode` 的编解码链路。

### 3.3 兼容：`ShowImage(ReceiveBitmapEventArgs)`

```csharp
var args = new ReceiveBitmapEventArgs(0, bitmap);
int code = imageControl.ShowImage(args);
```

**所有权语义**：当返回 `0` 时，控件接管该 `bitmap` 生命周期，调用方不要再释放或访问它。

## 4. 相机过滤规则

控件只显示 `NeedShowCam` 中包含的 `CamID`：

```csharp
imageControl.NeedShowCam = new[] { 0, 1 };
```

或构造时设置：

```csharp
var imageControl = new CtlOnlyShowImage(0, 1);
```

注意：`NeedShowCam` 为空数组时，不接收任何图像（`ShowImage*` 返回 `-1`）。

补充：`ShowImageFromStream` / `ShowImageCopy` 会先做相机 ID 过滤，再执行解码或复制。

## 5. 图元使用

### 5.1 坐标约定

- `PaintElement.Pts` 使用图像像素坐标：`[x1, y1, x2, y2, ...]`
- 原点在图像左上角

### 5.2 添加图元

```csharp
var elements = new List<PaintElement>
{
    new PaintElement
    {
        Type = PaintElementType.Rectangle,
        Pts = new List<double> { 100, 80, 280, 200 },
        Color = Colors.Lime,
        LineWidth = 2,
        Visible = true
    },
    new PaintElement
    {
        Type = PaintElementType.Arrow,
        Pts = new List<double> { 50, 50, 200, 120 },
        Color = Colors.OrangeRed,
        LineWidth = 2,
        Visible = true
    }
};

imageControl.SetPaintElements(elements);
imageControl.CtlShowPaintStatus = ImageElementCtlStatus.ShowAll;
imageControl.ReFresh();
```

### 5.3 图元有效性规则（最小点数）

- `Point`/`Cross`/`Text`：至少 2 个数值（1 个点）
- `Line`/`Rectangle`/`Circle`/`Ellipse`/`Arrow`/`PolyLine`：至少 4 个数值
- `Polygon`/`Ring`/`Arc`：至少 6 个数值

## 6. 图元交互编辑能力

当前支持“选中 + 拖动 + 改大小”的图元：

- `Rectangle`
- `Ellipse`
- `Circle`
- `Line`
- `Arrow`
- `Polygon`

说明：

- 矩形/椭圆：8 个边框句柄
- 圆：中心点句柄 + 半径句柄
- 线段/箭头：起点与终点句柄
- 多边形：顶点句柄

## 7. 事件

### 7.1 `ImageClick`

用户在图像空白区域单击左键触发：

```csharp
imageControl.ImageClick += (s, e) =>
{
    // e.ControlPosition: 控件坐标
    // e.ImagePosition: 图像像素坐标
};
```

### 7.2 `ElementChanged`

图元由 API 或交互发生变化时触发：

```csharp
imageControl.ElementChanged += (s, e) =>
{
    // e.Action: Added/Updated/Removed/Selected/Cleared/Replaced
    // e.Source: Api 或 Interaction
    // e.Phase : Preview 或 Committed
};
```

## 8. 常用 API 返回码

### 8.1 图像输入

- `ShowImage*`：`0` 成功，`-1` 相机不匹配，`-2` 参数无效

### 8.2 图元管理

- `0`：成功
- `-1`：参数无效
- `-2`：索引越界
- `-3`：状态无效（如空列表下设置选中索引）

## 9. 辅助类 `ImageControlHelper`

常用快捷方法：

- `CreateImageControl(...)`
- `ShowImageFromFile(...)`（内部走 `ShowImageFromStream`）
- `ShowImageFromBitmap(...)`（内部走 `ShowImageCopy`）
- `AddPaintElements(...)`（在现有图元基础上追加，不覆盖）
- `ClearPaintElements(...)`

## 10. 兼容接口说明

以下接口为兼容保留，在当前纯像素模式下不参与真实标定换算：

- `SetCameraCalib(string)`
- `SetCameraCalib(double[])`
- `SetCameraCalibRef(double[])`
- `SetCameraCalib(Point, int, int)`
- `SetUpdateCameraPos(Func<Point>)`
- `ConvertImageToMachinePosition(Point)`（当前返回像素坐标并做边界裁剪）

## 11. 常见问题

### 图元设置后不显示

请确认：

- `CtlShowPaintStatus` 已设为 `ShowAll` 或 `ShowSelected`
- 图元 `Visible = true`
- `Pts` 点数符合最小要求
- 图元坐标与图像坐标系一致（像素坐标）

补充：图元绘制已取消“点在可视区内才绘制”的旧裁剪逻辑；即使仅部分落入视口，也会正常显示可见部分。

### 图像不显示

请确认：

- `NeedShowCam` 包含当前 `CamID`
- 图像输入返回码为 `0`

### 窗口 resize 后显示异常

当前版本在控件尺寸变化时会自动更新视口状态：

- 若当前处于默认缩放（fit）状态，会随窗口大小重新适配。
- 若当前是用户放大状态，会保持放大比例并自动夹紧平移边界。
- 双击会复位到当前窗口下的 fit 视图。

## 12. 打包

```bash
dotnet pack -c Release
```

默认输出到 `bin/Release`。

