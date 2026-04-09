# AvaloniaVisionControl 使用文档

本文面向 `UserControlApp` 与其它 Avalonia 项目开发者，说明如何集成并使用 `CtlOnlyShowImage`。

## 1. 功能概览

- 图像显示（棋盘背景）
- 鼠标滚轮缩放、左键拖拽平移、双击复位
- 图元叠加绘制（点/线/圆/矩形/椭圆/多边形/文本/箭头等）
- 图元交互编辑（选中、拖动、句柄缩放）
- 图元变化事件回调（API 修改与鼠标交互都可监听）

当前坐标模式为纯图像像素坐标（`PaintElement.Pts` 按像素填写）。

## 2. 接入方式

### 2.1 项目引用

```xml
<ItemGroup>
  <ProjectReference Include="..\AvaloniaVisionControl\AvaloniaVisionControl.csproj" />
</ItemGroup>
```

### 2.2 XAML 放置控件

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vision="using:AvaloniaVisionControl"
        x:Class="YourApp.MainWindow">
  <vision:CtlOnlyShowImage x:Name="ImageControl"/>
</Window>
```

### 2.3 C# 初始化

```csharp
using Avalonia;
using AvaloniaVisionControl;

var imageControl = this.FindControl<CtlOnlyShowImage>("ImageControl");
if (imageControl != null)
{
    imageControl.AllowMouseScroll = true;
    imageControl.NeedShowCam = new[] { 0 }; // 仅接收 CamID=0
    imageControl.CtlShowPaintStatus = ImageElementCtlStatus.ShowAll;
    imageControl.IsElementEditingEnabled = true;

    // 兼容接口，当前纯像素模式下不参与实际换算，可按需调用
    imageControl.SetCameraCalib(new Point(0.1, 0.1), 1024, 768);
}
```

常用交互属性：

- `AllowMouseScroll`：是否允许滚轮缩放
- `CtlShowPaintStatus`：图元显示模式
- `IsElementEditingEnabled`：是否允许图元选中、拖动与句柄编辑

## 3. 显示图像

推荐优先使用 `ShowImageFromStream` 或 `ShowImageCopy`：

```csharp
using var stream = File.OpenRead(filePath);
int code = imageControl.ShowImageFromStream(0, stream);
```

或

```csharp
int code = imageControl.ShowImageCopy(0, bitmap);
```

也可使用兼容接口：

```csharp
var args = new ReceiveBitmapEventArgs(0, bitmap);
int code = imageControl.ShowImage(args);
```

返回码：

- `0`：成功
- `-1`：`CamID` 不在 `NeedShowCam` 中
- `-2`：参数或图像数据无效

## 4. 图元绘制

```csharp
var elements = new List<PaintElement>
{
    new PaintElement
    {
        Type = PaintElementType.Rectangle,
        Pts = new List<double> { 80, 80, 220, 170 },
        Color = Colors.LimeGreen,
        LineWidth = 1.5,
        Visible = true
    },
    new PaintElement
    {
        Type = PaintElementType.Circle,
        Pts = new List<double> { 260, 270, 330, 270 },
        Color = Colors.Red,
        LineWidth = 2,
        Visible = true
    },
    new PaintElement
    {
        Type = PaintElementType.Text,
        Pts = new List<double> { 80, 40 },
        Text = "可拖动图元演示",
        FontSize = 16,
        Color = Colors.Yellow,
        Visible = true
    }
};

imageControl.SetPaintElements(elements);
imageControl.CtlShowPaintStatus = ImageElementCtlStatus.ShowAll;
imageControl.ReFresh();
```

图元基本规则：

- `Pts` 必须成对（`x,y`）
- `Rectangle/Circle/Ellipse/Line/Arrow` 至少 4 个值
- `Polygon/Ring/Arc` 至少 6 个值
- `Text/Point/Cross` 至少 2 个值

## 5. 图元交互编辑

### 5.1 编辑开关

```csharp
imageControl.IsElementEditingEnabled = true;
```

行为说明：

- `true`：允许图元选中、拖动、句柄缩放
- `false`：禁止图元编辑，但仍允许图像滚轮缩放、平移、双击复位
- 编辑关闭时不显示编辑句柄

### 5.2 可拖动/可编辑图元

当前支持“选中 + 拖动 + 改大小”的图元：

- `Rectangle`
- `Ellipse`
- `Circle`
- `Line`
- `Arrow`
- `Polygon`

说明：

- 图元主体可拖动平移
- 句柄可缩放/改形状（如圆半径、线段端点、多边形顶点）

### 5.3 仅支持整体移动的图元

以下图元现在也支持鼠标选中与整体拖动，但没有句柄缩放：

- `Text`
- `Cross`
- `Point`

说明：

- `Text` 按文本包围框命中
- `Cross` 和 `Point` 按中心点附近容差命中
- 这三类拖动时同样会触发 `ElementChanged`

### 5.4 键盘交互

当控件获得焦点且 `IsElementEditingEnabled = true` 时：

- `Delete`：删除当前选中图元
- `Escape`：
  - 若当前正在拖动/缩放，取消本次交互并回滚
  - 若当前没有活动交互但存在选中图元，则清空选择

## 6. 事件与回调

### 6.1 图像点击事件

```csharp
imageControl.ImageClick += (s, e) =>
{
    // e.ControlPosition: 控件坐标
    // e.ImagePosition: 图像像素坐标
};
```

触发规则：

- 在图像区域内单击左键（未发生拖动）时触发
- 点击空白区域或图元本体都会触发
- 拖动图像或拖动图元时不触发

### 6.2 图元变更事件

```csharp
imageControl.ElementChanged += (s, e) =>
{
    // e.Action: Added/Updated/Removed/Selected/Cleared/Replaced
    // e.Source: Api / Interaction
    // e.Phase : Preview / Committed
    // e.Before / e.After: 变更前后快照
};
```

### 6.3 鼠标按下/松开坐标事件（ROI 接入）

```csharp
imageControl.ImageMouseDown += (s, e) =>
{
    // ROI 起点：e.ImagePosition
};

imageControl.ImageMouseUp += (s, e) =>
{
    // ROI 终点：e.ImagePosition
};
```

触发规则：

- `ImageMouseDown`：左键在图像区域内按下时触发
- `ImageMouseUp`：左键释放时触发（坐标会裁剪到图像范围）

## 7. 常用 API 清单

- 图像：
  - `ShowImage(...)`
  - `ShowImageFromStream(...)`
  - `ShowImageCopy(...)`
- 相机过滤：
  - `NeedShowCam`（属性）
  - `GetShowCam()`
- 图元：
  - `SetPaintElements(...)`
  - `AddPaintElement(...)`
  - `InsertPaintElement(...)`
  - `ChangePaintElement(...)`
  - `RemovePaintElementAt(...)`
  - `ClearPaintElements()`
  - `GetPaintElementsSnapshot()`
  - `SetSelectedElementIndex(...)`
  - `GetSelectedElementIndex()`
  - `ReFresh()`
- 交互：
  - `IsElementEditingEnabled`（属性）

## 8. 与 UserControlApp 对齐建议

- 参考 `UserControlApp/Views/Window3.axaml(.cs)` 做集成。
- 建议业务侧统一使用像素坐标管理图元，避免混用机械坐标。
- 若需保存/恢复图元，可直接序列化 `PaintElement` 列表（含 `Type/Pts/Color/Text/...`）。

当前 `Window3` 已被更新为验证页，可直接用来检查：

- 编辑开关启用/关闭
- `Rectangle/Ellipse/Circle/Line/Arrow/Polygon` 的句柄编辑
- `Text/Cross/Point` 的整体移动
- `Delete` / `Escape` 键盘行为
- `ImageClick` 与 `ElementChanged` 日志输出

## 9. 常见问题

### 图元不显示

- `CtlShowPaintStatus` 是否为 `ShowAll` 或 `ShowSelected`
- 图元 `Visible` 是否为 `true`
- `Pts` 数量是否满足该类型最小要求
- 是否误将 `IsElementEditingEnabled` 理解为“控制显示”

补充：

- `IsElementEditingEnabled` 只控制编辑，不影响图元是否显示
- 即使编辑关闭，已选中的图元仍保留选中高亮，但不会显示句柄

### 图像不显示

- `NeedShowCam` 是否包含当前 `CamID`
- `ShowImage*` 返回码是否为 `0`
