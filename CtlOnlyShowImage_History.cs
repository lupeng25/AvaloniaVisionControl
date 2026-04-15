using System;
using System.Collections.Generic;

namespace AvaloniaVisionControl
{
    public partial class CtlOnlyShowImage
    {
        private readonly struct SelectionState
        {
            // 记录主选中元素索引以及完整的选中索引集合。
            public SelectionState(int primaryIndex, List<int> selectedIndexes)
            {
                PrimaryIndex = primaryIndex;
                SelectedIndexes = selectedIndexes;
            }

            public int PrimaryIndex { get; }

            public List<int> SelectedIndexes { get; }
        }

        private sealed class HistoryEntry
        {
            public required List<PaintElement> BeforeElements { get; init; }
            public required List<PaintElement> AfterElements { get; init; }
            public required SelectionState BeforeSelection { get; init; }
            public required SelectionState AfterSelection { get; init; }
        }

        private readonly List<HistoryEntry> _undoHistory = new List<HistoryEntry>();
        private readonly List<HistoryEntry> _redoHistory = new List<HistoryEntry>();
        private List<PaintElement>? _copiedElements;
        private bool _isApplyingHistory;
        private int _maxHistoryEntries = 100;
        private bool _lastCanUndo;
        private bool _lastCanRedo;

        private const int PasteOffsetPixels = 10;

        public bool CanUndo => _undoHistory.Count > 0;

        public bool CanRedo => _redoHistory.Count > 0;

        public int MaxHistoryEntries
        {
            get => _maxHistoryEntries;
            set
            {
                int normalized = value < 1 ? 1 : value;
                if (_maxHistoryEntries == normalized)
                {
                    return;
                }

                _maxHistoryEntries = normalized;
                TrimHistoryToMax(_undoHistory);
                TrimHistoryToMax(_redoHistory);
                RaiseHistoryStateChangedIfNeeded();
            }
        }

        public event EventHandler? HistoryStateChanged;

        /// <summary>
        /// 恢复到上一条已提交快照，并将当前状态转入重做历史。
        /// </summary>
        public bool Undo()
        {
            if (!CanUndo)
            {
                return false;
            }

            HistoryEntry entry = PopLast(_undoHistory);
            ApplyHistorySnapshot(entry.BeforeElements, entry.BeforeSelection);
            PushHistory(_redoHistory, entry);
            RaiseHistoryStateChangedIfNeeded();
            return true;
        }

        /// <summary>
        /// 重新应用最近一次撤销的快照，并将其放回撤销历史。
        /// </summary>
        public bool Redo()
        {
            if (!CanRedo)
            {
                return false;
            }

            HistoryEntry entry = PopLast(_redoHistory);
            ApplyHistorySnapshot(entry.AfterElements, entry.AfterSelection);
            PushHistory(_undoHistory, entry);
            RaiseHistoryStateChangedIfNeeded();
            return true;
        }

        /// <summary>
        /// 将当前选中的全部元素复制到内部剪贴板。
        /// </summary>
        public bool CopySelectedElement()
        {
            List<int> selectedIndexes = GetOrderedSelectedElementIndexes();
            if (selectedIndexes.Count == 0)
            {
                return false;
            }

            _copiedElements = new List<PaintElement>(selectedIndexes.Count);
            foreach (int index in selectedIndexes)
            {
                _copiedElements.Add(m_CurrShowElement[index].DeepCopy());
            }

            return true;
        }

        /// <summary>
        /// 将剪贴板元素按小偏移量粘贴、更新选中状态，并记录一条历史。
        /// </summary>
        public bool PasteCopiedElement()
        {
            if (_copiedElements == null || _copiedElements.Count == 0)
            {
                return false;
            }

            var pastedElements = new List<PaintElement>(_copiedElements.Count);
            foreach (PaintElement copied in _copiedElements)
            {
                PaintElement pasted = copied.DeepCopy();
                OffsetElementPoints(pasted, PasteOffsetPixels, PasteOffsetPixels);
                if (!IsValidElement(pasted))
                {
                    return false;
                }

                pastedElements.Add(pasted);
            }

            List<PaintElement> beforeElements = CloneCurrentElements();
            SelectionState beforeSelection = CaptureSelectionState();

            int firstInsertedIndex = m_CurrShowElement.Count;
            foreach (PaintElement pasted in pastedElements)
            {
                m_CurrShowElement.Add(pasted);
            }

            int previousSelectedIndex = _selectedElementIndex;
            _selectedElementIndexes.Clear();
            for (int i = 0; i < pastedElements.Count; i++)
            {
                _selectedElementIndexes.Add(firstInsertedIndex + i);
            }
            _selectedElementIndex = firstInsertedIndex + pastedElements.Count - 1;

            for (int i = 0; i < pastedElements.Count; i++)
            {
                int insertedIndex = firstInsertedIndex + i;
                RaiseElementChanged(
                    PaintElementChangeAction.Added,
                    insertedIndex,
                    null,
                    pastedElements[i],
                    PaintElementChangeSource.Api,
                    PaintElementChangePhase.Committed);
            }

            RaiseSelectionChangedIfNeeded(
                previousSelectedIndex,
                _selectedElementIndex,
                PaintElementChangeSource.Api);

            InvalidateVisual();

            RecordHistoryCommittedChange(
                PaintElementChangeAction.Added,
                beforeElements,
                beforeSelection,
                CloneCurrentElements(),
                CaptureSelectionState());

            return true;
        }

        // 将一次已提交的变更写入撤销历史，并清空重做历史。
        // 在撤销/重做回放快照期间不记录历史。
        private void RecordHistoryCommittedChange(
            PaintElementChangeAction action,
            List<PaintElement> beforeElements,
            SelectionState beforeSelection,
            List<PaintElement> afterElements,
            SelectionState afterSelection)
        {
            if (_isApplyingHistory)
            {
                return;
            }

            if (!ShouldRecordAction(action))
            {
                return;
            }

            var entry = new HistoryEntry
            {
                BeforeElements = DeepCloneElements(beforeElements),
                AfterElements = DeepCloneElements(afterElements),
                BeforeSelection = NormalizeSelectionState(beforeSelection, beforeElements.Count),
                AfterSelection = NormalizeSelectionState(afterSelection, afterElements.Count)
            };

            PushHistory(_undoHistory, entry);
            _redoHistory.Clear();
            RaiseHistoryStateChangedIfNeeded();
        }

        // 克隆当前元素列表，避免历史快照被后续编辑影响。
        private List<PaintElement> CloneCurrentElements()
        {
            return DeepCloneElements(m_CurrShowElement);
        }

        // 按原顺序深拷贝 source 中的所有元素。
        private static List<PaintElement> DeepCloneElements(IReadOnlyList<PaintElement> source)
        {
            var result = new List<PaintElement>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                result.Add(source[i].DeepCopy());
            }

            return result;
        }

        // 定义哪些已提交动作需要生成撤销/重做历史项。
        private static bool ShouldRecordAction(PaintElementChangeAction action)
        {
            return action == PaintElementChangeAction.Added ||
                action == PaintElementChangeAction.Updated ||
                action == PaintElementChangeAction.Removed ||
                action == PaintElementChangeAction.Cleared ||
                action == PaintElementChangeAction.Replaced;
        }

        // 确保选中索引落在有效范围；-1 表示“无选中”。
        private static int NormalizeSelectedIndex(int index, int elementCount)
        {
            if (elementCount <= 0)
            {
                return -1;
            }

            if (index < 0)
            {
                return -1;
            }

            return index < elementCount ? index : elementCount - 1;
        }

        // 按当前元素数量规范化选中状态，并去重索引。
        private static SelectionState NormalizeSelectionState(SelectionState state, int elementCount)
        {
            var normalizedIndexes = new List<int>();
            if (elementCount > 0)
            {
                foreach (int index in state.SelectedIndexes)
                {
                    if (index >= 0 && index < elementCount && !normalizedIndexes.Contains(index))
                    {
                        normalizedIndexes.Add(index);
                    }
                }
            }

            normalizedIndexes.Sort();
            int primaryIndex = NormalizeSelectedIndex(state.PrimaryIndex, elementCount);
            if (primaryIndex >= 0 && !normalizedIndexes.Contains(primaryIndex))
            {
                normalizedIndexes.Add(primaryIndex);
                normalizedIndexes.Sort();
            }

            return new SelectionState(primaryIndex, normalizedIndexes);
        }

        // 对元素点位列表中的每组坐标应用平移偏移。
        private static void OffsetElementPoints(PaintElement element, double dx, double dy)
        {
            for (int i = 0; i + 1 < element.Pts.Count; i += 2)
            {
                element.Pts[i] += dx;
                element.Pts[i + 1] += dy;
            }
        }

        // 捕获当前选中状态，用于历史快照。
        private SelectionState CaptureSelectionState()
        {
            return new SelectionState(_selectedElementIndex, GetOrderedSelectedElementIndexes());
        }

        // 从历史快照恢复当前元素与选中状态，并触发已提交变更通知。
        private void ApplyHistorySnapshot(List<PaintElement> elements, SelectionState selection)
        {
            int previousSelectedIndex = _selectedElementIndex;

            _isApplyingHistory = true;
            try
            {
                m_CurrShowElement = DeepCloneElements(elements);
                SelectionState normalizedSelection = NormalizeSelectionState(selection, m_CurrShowElement.Count);
                ReplaceSelectedIndexesInternal(
                    normalizedSelection.SelectedIndexes,
                    normalizedSelection.PrimaryIndex);
                ResetInteractionState();

                RaiseElementChanged(
                    PaintElementChangeAction.Replaced,
                    -1,
                    null,
                    null,
                    PaintElementChangeSource.Api,
                    PaintElementChangePhase.Committed);

                RaiseSelectionChangedIfNeeded(
                    previousSelectedIndex,
                    _selectedElementIndex,
                    PaintElementChangeSource.Api);

                InvalidateVisual();
            }
            finally
            {
                _isApplyingHistory = false;
            }
        }

        // 从栈式历史列表中弹出最新一条记录。
        private static HistoryEntry PopLast(List<HistoryEntry> stack)
        {
            int lastIndex = stack.Count - 1;
            HistoryEntry result = stack[lastIndex];
            stack.RemoveAt(lastIndex);
            return result;
        }

        // 压入一条历史记录，并按 MaxHistoryEntries 裁剪超出部分。
        private void PushHistory(List<HistoryEntry> stack, HistoryEntry entry)
        {
            stack.Add(entry);
            TrimHistoryToMax(stack);
        }

        // 当历史数量超出上限时，移除最旧记录。
        private void TrimHistoryToMax(List<HistoryEntry> stack)
        {
            while (stack.Count > _maxHistoryEntries)
            {
                stack.RemoveAt(0);
            }
        }

        // 仅在 CanUndo/CanRedo 状态发生变化时触发历史状态事件。
        private void RaiseHistoryStateChangedIfNeeded()
        {
            bool canUndo = CanUndo;
            bool canRedo = CanRedo;
            if (canUndo == _lastCanUndo && canRedo == _lastCanRedo)
            {
                return;
            }

            _lastCanUndo = canUndo;
            _lastCanRedo = canRedo;
            HistoryStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
