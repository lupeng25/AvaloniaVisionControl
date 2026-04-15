using System;
using System.Collections.Generic;

namespace AvaloniaVisionControl
{
    /// <summary>
    /// 图元编辑接口，定义增删改查与选中控制能力。
    /// </summary>
    public interface IEditablePaintElement
    {
        /// <summary>
        /// 图元变更事件（支持预览阶段和提交阶段）。
        /// </summary>
        event EventHandler<PaintElementChangedEventArgs>? ElementChanged;

        /// <summary>
        /// 追加一个图元。
        /// </summary>
        /// <returns>0 表示成功，负数表示失败。</returns>
        int AddPaintElement(PaintElement element);

        /// <summary>
        /// 在指定索引处插入图元。
        /// </summary>
        /// <returns>0 表示成功，负数表示失败。</returns>
        int InsertPaintElement(int index, PaintElement element);

        /// <summary>
        /// 删除指定索引处的图元。
        /// </summary>
        /// <returns>0 表示成功，负数表示失败。</returns>
        int RemovePaintElementAt(int index);

        /// <summary>
        /// 清空当前全部图元。
        /// </summary>
        /// <returns>0 表示成功，负数表示失败。</returns>
        int ClearPaintElements();

        /// <summary>
        /// 获取图元快照副本，避免外部直接修改内部集合。
        /// </summary>
        IReadOnlyList<PaintElement> GetPaintElementsSnapshot();

        /// <summary>
        /// 设置当前选中的图元索引。
        /// </summary>
        /// <returns>0 表示成功，负数表示失败。</returns>
        int SetSelectedElementIndex(int index);

        /// <summary>
        /// 获取当前主选中图元索引。
        /// </summary>
        int GetSelectedElementIndex();
    }
}
