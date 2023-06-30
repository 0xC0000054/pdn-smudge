using PaintDotNet;
using System.Drawing;
using System.IO;

namespace pyrochild.effects.common
{
    internal static class PngBrushReader
    {
        public static SmudgeBrush Load(string path, string brushCacheFolder)
        {
            SmudgeBrush brush = null;
            try
            {
                Surface surface = null;
                using (Bitmap b = new Bitmap(path))
                {
                    surface = Surface.CopyFromBitmap(b);
                }

                brush = new SmudgeBrush(Path.GetFileNameWithoutExtension(path), surface, brushCacheFolder);
            }
            catch { }

            return brush;
        }
    }
}
