using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace AvaloniaVisionControl
{
    public partial class CtlOnlyShowImage
    {
        private sealed class SnapshotRenderSurface : Control
        {
            private readonly Bitmap _image;
            private readonly IReadOnlyList<PaintElement> _elements;

            public SnapshotRenderSurface(Bitmap image, IReadOnlyList<PaintElement> elements)
            {
                _image = image;
                _elements = elements;
                Width = image.PixelSize.Width;
                Height = image.PixelSize.Height;
            }

            public override void Render(DrawingContext context)
            {
                base.Render(context);

                var imageRect = new Rect(0, 0, _image.PixelSize.Width, _image.PixelSize.Height);
                context.DrawImage(_image, imageRect);

                for (int i = 0; i < _elements.Count; i++)
                {
                    PaintElement element = _elements[i];
                    if (!element.Visible)
                    {
                        continue;
                    }

                    var transformedPoints = new List<float>(element.Pts.Count);
                    for (int pointIndex = 0; pointIndex < element.Pts.Count; pointIndex++)
                    {
                        transformedPoints.Add((float)element.Pts[pointIndex]);
                    }

                    element.Paint(context, 1.0, transformedPoints);
                }
            }
        }

        public bool ExportSnapshot(Stream outputStream)
        {
            if (outputStream == null || !outputStream.CanWrite || _originImage == null)
            {
                return false;
            }

            var renderElements = new List<PaintElement>();
            for (int i = 0; i < m_CurrShowElement.Count; i++)
            {
                if (ShouldRenderElement(i))
                {
                    renderElements.Add(m_CurrShowElement[i].DeepCopy());
                }
            }

            var renderSurface = new SnapshotRenderSurface(_originImage, renderElements);
            var pixelSize = new PixelSize(_originImage.PixelSize.Width, _originImage.PixelSize.Height);
            var renderTarget = new RenderTargetBitmap(pixelSize, new Vector(96, 96));
            renderSurface.Measure(new Size(pixelSize.Width, pixelSize.Height));
            renderSurface.Arrange(new Rect(0, 0, pixelSize.Width, pixelSize.Height));
            renderTarget.Render(renderSurface);
            renderTarget.Save(outputStream);
            return true;
        }

        public bool ExportSnapshot(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var stream = File.Create(filePath);
            return ExportSnapshot(stream);
        }
    }
}
