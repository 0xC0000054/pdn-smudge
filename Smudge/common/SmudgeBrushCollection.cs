using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using PaintDotNet;

namespace pyrochild.effects.common
{
    public class SmudgeBrushCollection : IEnumerable<SmudgeBrush>, IList<SmudgeBrush>, ICollection<SmudgeBrush>, IDisposable
    {
        private List<SmudgeBrush> brushes;
        private static string brushpath;

        public SmudgeBrushCollection(IServiceProvider serviceprovider, string ownername)
        {
            brushpath = Path.Combine(serviceprovider.GetService<PaintDotNet.AppModel.IUserFilesService>().UserFilesPath, ownername + " Brushes");
            brushes = new List<SmudgeBrush>();

            if (Directory.Exists(BrushesPath))
            {
                string[] filenames = Directory.GetFiles(BrushesPath, "*.png", SearchOption.TopDirectoryOnly);
                foreach (string s in filenames)
                {
                    string filename = Path.GetFileNameWithoutExtension(s);

                    SmudgeBrush brush = new SmudgeBrush(filename);
                    if (!brushes.Contains(brush))
                    {
                        brushes.Add(new SmudgeBrush(filename));
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
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}