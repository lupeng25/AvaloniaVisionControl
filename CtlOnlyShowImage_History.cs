using System;
using System.Collections.Generic;

namespace AvaloniaVisionControl
{
    public partial class CtlOnlyShowImage
    {
        private sealed class HistoryEntry
        {
            public required List<PaintElement> BeforeElements { get; init; }
            public required List<PaintElement> AfterElements { get; init; }
            public required int BeforeSelectedIndex { get; init; }
            public required int AfterSelectedIndex { get; init; }
        }

        private readonly List<HistoryEntry> _undoHistory = new List<HistoryEntry>();
        private readonly List<HistoryEntry> _redoHistory = new List<HistoryEntry>();
        private PaintElement? _copiedElement;
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
            ApplyHistorySnapshot(entry.BeforeElements, entry.BeforeSelectedIndex);
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
            ApplyHistorySnapshot(entry.AfterElements, entry.AfterSelectedIndex);
            PushHistory(_undoHistory, entry);
            RaiseHistoryStateChangedIfNeeded();
            return true;
        }

        public bool CopySelectedElement()
        {
            if (!IsIndexValid(_selectedElementIndex))
            {
                return false;
            }

            _copiedElement = m_CurrShowElement[_selectedElementIndex].DeepCopy();
            return true;
        }

        public bool PasteCopiedElement()
        {
            if (_copiedElement == null)
            {
                return false;
            }

            PaintElement pasted = _copiedElement.DeepCopy();
            OffsetElementPoints(pasted, PasteOffsetPixels, PasteOffsetPixels);
            if (!IsValidElement(pasted))
            {
                return false;
            }

            List<PaintElement> beforeElements = CloneCurrentElements();
            int beforeSelectedIndex = _selectedElementIndex;

            m_CurrShowElement.Add(pasted);
            int insertedIndex = m_CurrShowElement.Count - 1;
            int previousSelectedIndex = _selectedElementIndex;
            _selectedElementIndex = insertedIndex;

            RaiseElementChanged(
                PaintElementChangeAction.Added,
                insertedIndex,
                null,
                pasted,
                PaintElementChangeSource.Api,
                PaintElementChangePhase.Committed);

            RaiseSelectionChangedIfNeeded(
                previousSelectedIndex,
                _selectedElementIndex,
                PaintElementChangeSource.Api);

            InvalidateVisual();

            RecordHistoryCommittedChange(
                PaintElementChangeAction.Added,
                beforeElements,
                beforeSelectedIndex,
                CloneCurrentElements(),
                _selectedElementIndex);

            return true;
        }

        private void RecordHistoryCommittedChange(
            PaintElementChangeAction action,
            List<PaintElement> beforeElements,
            int beforeSelectedIndex,
            List<PaintElement> afterElements,
            int afterSelectedIndex)
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
                BeforeSelectedIndex = NormalizeSelectedIndex(beforeSelectedIndex, beforeElements.Count),
                AfterSelectedIndex = NormalizeSelectedIndex(afterSelectedIndex, afterElements.Count)
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

        private static void OffsetElementPoints(PaintElement element, double dx, double dy)
        {
            for (int i = 0; i + 1 < element.Pts.Count; i += 2)
            {
                element.Pts[i] += dx;
                element.Pts[i + 1] += dy;
            }
        }

        private void ApplyHistorySnapshot(List<PaintElement> elements, int selectedIndex)
        {
            int previousSelectedIndex = _selectedElementIndex;

            _isApplyingHistory = true;
            try
            {
                m_CurrShowElement = DeepCloneElements(elements);
                _selectedElementIndex = NormalizeSelectedIndex(selectedIndex, m_CurrShowElement.Count);
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
