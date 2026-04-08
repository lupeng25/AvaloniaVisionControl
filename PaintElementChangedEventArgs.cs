using System;

namespace AvaloniaVisionControl
{
    public enum PaintElementChangeAction
    {
        Added,
        Updated,
        Removed,
        Selected,
        Cleared,
        Replaced
    }

    public enum PaintElementChangeSource
    {
        Api,
        Interaction
    }

    public enum PaintElementChangePhase
    {
        Preview,
        Committed
    }

    public sealed class PaintElementChangedEventArgs : EventArgs
    {
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

        public PaintElementChangeAction Action { get; }

        public int Index { get; }

        public PaintElement? Before { get; }

        public PaintElement? After { get; }

        public PaintElementChangeSource Source { get; }

        public PaintElementChangePhase Phase { get; }
    }
}
