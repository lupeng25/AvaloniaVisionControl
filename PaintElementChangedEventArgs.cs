using System;

namespace AvaloniaVisionControl
{
    /// <summary>
    /// 图元变更动作类型。
    /// </summary>
    public enum PaintElementChangeAction
    {
        /// <summary>新增图元。</summary>
        Added,
        /// <summary>更新图元。</summary>
        Updated,
        /// <summary>删除图元。</summary>
        Removed,
        /// <summary>选中状态变化。</summary>
        Selected,
        /// <summary>清空图元集合。</summary>
        Cleared,
        /// <summary>整体替换图元集合。</summary>
        Replaced
    }

    /// <summary>
    /// 图元变更来源。
    /// </summary>
    public enum PaintElementChangeSource
    {
        /// <summary>由 API 调用触发。</summary>
        Api,
        /// <summary>由用户交互触发。</summary>
        Interaction
    }

    /// <summary>
    /// 图元变更阶段。
    /// </summary>
    public enum PaintElementChangePhase
    {
        /// <summary>交互预览阶段，结果尚未最终提交。</summary>
        Preview,
        /// <summary>已提交阶段，结果已生效。</summary>
        Committed
    }

    /// <summary>
    /// 图元变更事件参数。
    /// </summary>
    public sealed class PaintElementChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 创建图元变更事件参数。
        /// </summary>
        public PaintElementChangedEventArgs(
            PaintElementChangeAction action,
            int index,
            PaintElement? before,
            PaintElement? after,
            PaintElementChangeSource source,
            PaintElementChangePhase phase)
        {
            Action = action;
            Index = index;
            Before = before;
            After = after;
            Source = source;
            Phase = phase;
        }

        /// <summary>
        /// 变更动作类型。
        /// </summary>
        public PaintElementChangeAction Action { get; }

        /// <summary>
        /// 变更目标索引；为 -1 时通常表示集合级变更。
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// 变更前图元（无则为 null）。
        /// </summary>
        public PaintElement? Before { get; }

        /// <summary>
        /// 变更后图元（无则为 null）。
        /// </summary>
        public PaintElement? After { get; }

        /// <summary>
        /// 变更来源（API 或交互）。
        /// </summary>
        public PaintElementChangeSource Source { get; }

        /// <summary>
        /// 变更阶段（预览或提交）。
        /// </summary>
        public PaintElementChangePhase Phase { get; }
    }
}
