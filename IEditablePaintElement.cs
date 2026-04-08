using System;
using System.Collections.Generic;

namespace AvaloniaVisionControl
{
    public interface IEditablePaintElement
    {
        event EventHandler<PaintElementChangedEventArgs>? ElementChanged;

        int AddPaintElement(PaintElement element);

        int InsertPaintElement(int index, PaintElement element);

        int RemovePaintElementAt(int index);

        int ClearPaintElements();

        IReadOnlyList<PaintElement> GetPaintElementsSnapshot();

        int SetSelectedElementIndex(int index);

        int GetSelectedElementIndex();
    }
}
