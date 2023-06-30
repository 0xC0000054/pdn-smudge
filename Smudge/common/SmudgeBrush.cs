using PaintDotNet;
using System;
using System.Drawing;

namespace pyrochild.effects.common
{
    public sealed class SmudgeBrush : IEquatable<SmudgeBrush>
    {
        private DiskBackedSurface original;

        public static bool operator ==(SmudgeBrush left, SmudgeBrush right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null || right is null)
            {
                return false;
            }

            return left.Equals(right);
        }

        public static bool operator !=(SmudgeBrush left, SmudgeBrush right)
        {
            return !(left == right);
        }

        public SmudgeBrush(string brushName, Surface surface, string cacheFolder)
        {
            name = brushName;
            original = new DiskBackedSurface(surface, true, cacheFolder);
            nativesize = surface.Size;
            thumbnailalphaonly = GetSurfaceAlphaOnly(32);
        }

        public void Dispose()
        {
            DisposableUtil.Free(ref original);
            DisposableUtil.Free(ref thumbnailalphaonly);
        }

        public override bool Equals(object obj)
        {
            return obj is SmudgeBrush other && Equals(other);
        }

        public bool Equals(SmudgeBrush other)
        {
            return other is not null && other.name == this.name;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            return Name;
        }

        private Surface thumbnailalphaonly;
        public Surface ThumbnailAlphaOnly
        {
            get
            {
                VerifyNotDisposed();

                return thumbnailalphaonly;
            }
        }

        private Size nativesize;
        public Size NativeSize { get { return nativesize; } }

        public string NativeSizePrettyString { get { return "(" + nativesize.Width.ToString() + "x" + nativesize.Height.ToString() + ")"; } }

        private string name;
        public string Name { get { return name; } }

        public Surface GetSurface(int maxsidelength)
        {
            VerifyNotDisposed();

            Size size;

            original.ToMemory();

            if (original.Width > original.Height)
            {
                size = new Size(
                    maxsidelength,
                    Math.Max((maxsidelength * original.Height) / original.Width, 1));
            }
            else
            {
                size = new Size(
                    Math.Max((maxsidelength * original.Width) / original.Height, 1),
                    maxsidelength);
            }

            var ret = new Surface(size);
            if (ret.Width <= original.Width
                && ret.Height <= original.Height)
            {
                ret.FitSurface(ResamplingAlgorithm.AdaptiveHighQuality, original.Surface);
            }
            else
            {
                ret.FitSurface(ResamplingAlgorithm.Cubic, original.Surface);
            }

            original.ToDisk();

            return ret;
        }

        public Surface GetSurface(Size size)
        {
            VerifyNotDisposed();

            var ret = new Surface(size);
            original.ToMemory();

            if (original.Width <= ret.Width
                && original.Height <= ret.Height)
            {
                ret.FitSurface(ResamplingAlgorithm.AdaptiveHighQuality, original.Surface);
            }
            else
            {
                ret.FitSurface(ResamplingAlgorithm.Cubic, original.Surface);
            }

            original.ToDisk();

            return ret;
        }

        public unsafe Surface GetSurfaceAlphaOnly(Size size)
        {
            Surface retval = GetSurface(size);
            AlphaOnly(retval);
            return retval;
        }

        unsafe private static void AlphaOnly(Surface retval)
        {
            ColorBgra* ptr = retval.GetRowPointerUnchecked(0);
            for (int y = 0; y < retval.Height; y++)
            {
                for (int x = 0; x < retval.Width; x++)
                {
                    ptr->Bgra = (uint)ptr->A << 24;
                    ptr++;
                }
            }
        }

        public unsafe Surface GetSurfaceAlphaOnly(int maxsidelength)
        {
            Surface retval = GetSurface(maxsidelength);
            AlphaOnly(retval);
            return retval;
        }

        private void VerifyNotDisposed()
        {
            if (original is null)
            {
                ExceptionUtil.ThrowObjectDisposedException(nameof(SmudgeBrush));
            }
        }
    }
}