using PaintDotNet;
using System;
using System.IO;

namespace pyrochild.effects.common
{
    internal static partial class SurfaceSerializer
    {
        private const int MaxChunkSize = 1024 * 1024 * 1024; // 1 GB

        public static unsafe Surface Deserialize(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            FileHeader header = new(stream);

            Surface surface = new(header.Width, header.Height);

            if (surface.Stride != header.Stride)
            {
                throw new FormatException("The header stride does not match the surface stride.");
            }

            long offset = 0;
            long surfaceLengthInBytes = surface.Scan0.Length;
            byte* scan0 = (byte*)surface.Scan0.VoidStar;

            while (offset < surfaceLengthInBytes)
            {
                int bytesToRead = (int)Math.Min(MaxChunkSize, surfaceLengthInBytes - offset);

                Span<byte> buffer = new(scan0 + offset, bytesToRead);

                stream.ReadExactly(buffer);

                offset += bytesToRead;
            }

            return surface;
        }

        public static unsafe void Serialize(Stream stream, Surface surface)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(surface);

            FileHeader header = new(surface.Width, surface.Height, surface.Stride);

            header.Save(stream);

            long offset = 0;
            long surfaceLengthInBytes = surface.Scan0.Length;
            byte* scan0 = (byte*)surface.Scan0.VoidStar;

            while (offset < surfaceLengthInBytes)
            {
                int bytesToWrite = (int)Math.Min(MaxChunkSize, surfaceLengthInBytes - offset);

                Span<byte> buffer = new(scan0 + offset, bytesToWrite);

                stream.Write(buffer);

                offset += bytesToWrite;
            }
        }
    }
}
