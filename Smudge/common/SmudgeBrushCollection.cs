using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using PaintDotNet;
using pyrochild.effects.smudge.Abr;

namespace pyrochild.effects.common
{
    public class SmudgeBrushCollection : IEnumerable<SmudgeBrush>, IList<SmudgeBrush>, ICollection<SmudgeBrush>, IDisposable
    {
        private List<SmudgeBrush> brushes;
        private readonly string cachefolder;
        private static string brushpath;

        public SmudgeBrushCollection(IServiceProvider serviceprovider, string ownername)
        {
            brushpath = Path.Combine(serviceprovider.GetService<PaintDotNet.AppModel.IUserFilesService>().UserFilesPath, ownername + " Brushes");
            brushes = new List<SmudgeBrush>();
            cachefolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(cachefolder);

            if (Directory.Exists(BrushesPath))
            {
                foreach (string path in Directory.EnumerateFiles(BrushesPath, "*", SearchOption.TopDirectoryOnly))
                {
                    if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        SmudgeBrush brush = PngBrushReader.Load(path, cachefolder);

                        if (brush != null && !brushes.Contains(brush))
                        {
                            brushes.Add(brush);
                        }
                    }
                    else if (path.EndsWith(".abr", StringComparison.OrdinalIgnoreCase))
                    {
                        List<SmudgeBrush> abrBrushes = AbrBrushReader.Load(path, cachefolder);

                        if (abrBrushes != null && abrBrushes.Count > 0)
                        {
                            foreach (SmudgeBrush brush in abrBrushes)
                            {
                                if (!brushes.Contains(brush))
                                {
                                    brushes.Add(brush);
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                try
                {
                    Directory.CreateDirectory(BrushesPath);
                }
                catch { }
            }
        }

        public static string BrushesPath
        {
            get
            {
                return brushpath;
            }
        }

        public int FindIndex(string name)
        {
            for (int i = 0; i < brushes.Count; i++)
            {
                if (brushes[i].Name.Equals(name, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        #region IEnumerable<PngBrush> Members

        public IEnumerator<SmudgeBrush> GetEnumerator()
        {
            return brushes.GetEnumerator();
        }

        #endregion

        #region IList<PngBrush> Members

        public int IndexOf(SmudgeBrush item)
        {
            return brushes.IndexOf(item);
        }

        public void Insert(int index, SmudgeBrush item)
        {
            brushes.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            brushes.RemoveAt(index);
        }

        public SmudgeBrush this[int index]
        {
            get
            {
                return brushes[index];
            }
            set
            {
                brushes[index] = value;
            }
        }

        #endregion

        #region ICollection<PngBrush> Members

        public void Add(SmudgeBrush item)
        {
            brushes.Add(item);
        }

        public void Clear()
        {
            brushes.Clear();
        }

        public bool Contains(SmudgeBrush item)
        {
            return brushes.Contains(item);
        }

        public void CopyTo(SmudgeBrush[] array, int arrayIndex)
        {
            brushes.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return brushes.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(SmudgeBrush item)
        {
            return brushes.Remove(item);
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator()
        {
            return brushes.GetEnumerator();
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            foreach (SmudgeBrush pb in brushes)
            {
                pb.Dispose();
            }
            Directory.Delete(cachefolder, true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}