# AvaloniaVisionControl Agent 指南

## 1. 目标

本文件用于让 AI 在 1 分钟内定位到正确代码位置，减少全量扫描和 token 消耗。

## 2. 最小阅读路径（低 Token）

1. `CtlOnlyShowImage.cs`
2. `CtlOnlyShowImage_Paint.cs`
3. `CtlOnlyShowImage_Edit.cs`
4. `PaintElement.cs`
5. `PaintStruct.cs`
6. `PaintElementChangedEventArgs.cs`
7. `USAGE.md`（接入示例）

如果只是确认对外行为，先读 `README.md` + `USAGE.md` 即可。

## 3. 核心结构

- 主控件：`CtlOnlyShowImage`（按职责拆为 partial）
- 文件职责：
  - `CtlOnlyShowImage.cs`：图像生命周期、缩放/平移/双击复位、属性定义、渲染入口、输入分发
  - `CtlOnlyShowImage_Paint.cs`：图元列表与 CRUD、合法性校验、选中态、变更事件、兼容标定 API
  - `CtlOnlyShowImage_Edit.cs`：命中检测、拖拽/句柄缩放、Preview/Committed 事件、键盘交互
- 图元模型：`PaintElement`
- 枚举状态：`PaintStruct.cs`
- 事件参数：`EventArgs.cs`、`PaintElementChangedEventArgs.cs`
- 辅助类：`ImageControlHelper.cs`

## 4. 关键约束（必须遵守）

- 当前坐标模式是纯图像像素坐标。
- `PaintElement.Pts` 格式固定为 `[x1, y1, x2, y2, ...]`，数量必须为偶数。
- `NeedShowCam` 决定图像是否接收；为空数组时全部拒收。
- `ShowImage` 所有权语义：
  - 返回 `0` 后，`Bitmap` 生命周期由控件接管，调用方不要再访问或释放。
- 推荐优先使用 `ShowImageFromStream` / `ShowImageCopy`。
- 标定相关 API 目前仅兼容保留，实际变换等效单位矩阵。

## 5. 任务到文件定位

- 改缩放/平移/双击复位：`CtlOnlyShowImage.cs`
- 改图像输入、返回码、相机过滤：`CtlOnlyShowImage.cs`
- 改图元校验/CRUD/选择逻辑：`CtlOnlyShowImage_Paint.cs`
- 改句柄、命中、拖拽缩放、光标：`CtlOnlyShowImage_Edit.cs`
- 改绘制风格与缓存性能：`PaintElement.cs`
- 改事件语义（Action/Source/Phase）：`PaintElementChangedEventArgs.cs` + 触发点代码

## 6. 交互语义速记

- `IsElementEditingEnabled = false`：禁用图元编辑，但图像平移/缩放仍可用。
- `Delete`：删除选中图元。
- `Escape`：优先取消当前拖拽/缩放；若无活动交互则清除选中。
- `ElementChanged` 在交互中分两阶段：
  - `Preview`：拖动/缩放进行中
  - `Committed`：鼠标释放后提交
- 双击行为：
  - 若存在选中图元，先清选中
  - 否则复位到 fit 视图

## 7. 返回码速记

- 图像输入 `ShowImage*`：
  - `0` 成功
  - `-1` 相机不匹配
  - `-2` 参数或图像数据无效
- 图元管理：
  - `0` 成功
  - `-1` 参数无效
  - `-2` 索引越界
  - `-3` 状态无效

## 8. 快速验证清单

- 编译：`dotnet build AvaloniaVisionControl.csproj`
- 功能检查（建议在 `UserControlApp`）：
  - 滚轮缩放、拖拽平移、双击复位
  - 图元选中、整体拖动、句柄缩放
  - `Text/Cross/Point` 仅整体移动
  - `Delete` / `Escape` 行为
  - `ElementChanged` 的 Preview + Committed

## 9. 后续 AI 建议流程

1. 先读本文件 + `FEATURE_CHANGES.md`
2. 按“任务到文件定位”只读取必要源码
3. 非明确需求下不要改兼容标定路径
4. 每次功能变更后向 `FEATURE_CHANGES.md` 追加记录
