# 功能改动文档

本文用于记录 `AvaloniaVisionControl` 的功能级变更，方便 AI 与开发者快速理解行为演进。

## 1. 当前能力基线（截至 2026-04-10）

### 1.1 控件与视口

- 主控件：`CtlOnlyShowImage`
- 棋盘背景 + 图像显示
- 鼠标滚轮缩放、左键拖拽平移、双击复位
- 支持控件尺寸变化后的 fit 重算与平移边界夹紧

### 1.2 图像输入

- `ShowImage(ReceiveBitmapEventArgs)`（成功后所有权转移）
- `ShowImageFromStream(int camId, Stream imageStream)`（安全解码路径）
- `ShowImageCopy(int camId, Bitmap sourceImage)`（安全复制路径）
- 相机过滤规则：
  - `NeedShowCam` 为空 => 拒收全部
  - 相机不匹配 => 返回 `-1`

### 1.3 图元能力

- 数据模型：`PaintElement`
- 已支持绘制类型：
  - `Point`, `Line`, `PolyLine`, `Circle`, `Rectangle`, `Ellipse`, `Polygon`, `Text`, `Cross`, `Arrow`, `Ring`, `Arc`
- 图元 CRUD 与选中管理：`CtlOnlyShowImage_Paint.cs`

### 1.4 交互编辑

- 支持句柄编辑的图元：
  - `Rectangle`, `Ellipse`, `Circle`, `Line`, `Arrow`, `Polygon`
- 仅支持整体移动的图元：
  - `Text`, `Cross`, `Point`
- 键盘：
  - `Delete` 删除当前选中
  - `Escape` 取消交互或清除选中

### 1.5 事件体系

- `ImageClick`
- `ImageMouseDown`
- `ImageMouseUp`
- `ElementChanged`
  - `Action`: Added/Updated/Removed/Selected/Cleared/Replaced
  - `Source`: Api/Interaction
  - `Phase`: Preview/Committed

### 1.6 坐标与兼容性

- 当前模式：纯图像像素坐标
- 标定相关 API 为兼容保留，当前实现等效单位变换

## 2. 变更记录模板（必填）

发生行为/API 变化时，请追加一条记录，格式如下：

```md
## [YYYY-MM-DD] <简短标题>
- Type: Feature | Behavior Change | Fix | Refactor | Docs
- Scope: <模块/文件列表>
- Summary:
  - <改动点1>
  - <改动点2>
- Compatibility:
  - <是否破坏兼容，迁移说明>
- Verification:
  - <验证方式或测试命令>
```

## 3. 变更记录

## [2026-04-10] 新增 AI 导航与改动记录文档
- Type: Docs
- Scope: `Agent.md`, `FEATURE_CHANGES.md`
- Summary:
  - 新增 `Agent.md`，提供低 token 的项目导航与任务到文件映射。
  - 新增 `FEATURE_CHANGES.md`，沉淀能力基线与后续变更记录规范。
- Compatibility:
  - 非破坏性文档更新。
- Verification:
  - 已对照 `CtlOnlyShowImage*.cs`、`PaintElement.cs`、`README.md`、`USAGE.md` 进行人工核对。
