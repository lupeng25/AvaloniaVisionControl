using System;
using System.Collections.Generic;

namespace AvaloniaVisionControl
{
    public partial class CtlOnlyShowImage
    {
        private readonly struct SelectionState
        {
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

        private List<PaintElement> CloneCurrentElements()
        {
            return DeepCloneElements(m_CurrShowElement);
        }

        private static List<PaintElement> DeepCloneElements(IReadOnlyList<PaintElement> source)
        {
            var result = new List<PaintElement>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                result.Add(source[i].DeepCopy());
            }

            return result;
        }

        private static bool ShouldRecordAction(PaintElementChangeAction action)
        {
            return action == PaintElementChangeAction.Added ||
                action == PaintElementChangeAction.Updated ||
                action == PaintElementChangeAction.Removed ||
                action == PaintElementChangeAction.Cleared ||
                action == PaintElementChangeAction.Replaced;
        }

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

        private static void OffsetElementPoints(PaintElement element, double dx, double dy)
        {
            for (int i = 0; i + 1 < element.Pts.Count; i += 2)
            {
                element.Pts[i] += dx;
                element.Pts[i + 1] += dy;
            }
        }

        private SelectionState CaptureSelectionState()
        {
            return new SelectionState(_selectedElementIndex, GetOrderedSelectedElementIndexes());
        }

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

        private static HistoryEntry PopLast(List<HistoryEntry> stack)
        {
            int lastIndex = stack.Count - 1;
            HistoryEntry result = stack[lastIndex];
            stack.RemoveAt(lastIndex);
            return result;
        }

        private void PushHistory(List<HistoryEntry> stack, HistoryEntry entry)
        {
            stack.Add(entry);
            TrimHistoryToMax(stack);
        }

        private void TrimHistoryToMax(List<HistoryEntry> stack)
        {
            while (stack.Count > _maxHistoryEntries)
            {
                stack.RemoveAt(0);
            }
        }

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
