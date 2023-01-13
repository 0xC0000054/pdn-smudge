using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace pyrochild.effects.common
{
    internal static partial class SurfaceSerializer
    {
        private sealed class FileHeader
        {
            private readonly int version;

            public FileHeader(Stream stream)
            {
                Span<byte> signature = stackalloc byte[4];

                stream.ReadExactly(signature);

                if (!FileSignature.SequenceEqual(signature))
                {
                    throw new FormatException("Unknown file signature.");
                }

                version = ReadInt32(stream);

                if (version != 1)
                {
                    throw new FormatException("Unknown file version.");
                }

                Width = ReadInt32(stream);
                Height = ReadInt32(stream);
                Stride = ReadInt32(stream);
            }

            public FileHeader(int width, int height, int stride)
            {
                version = 1;
                Width = width;
                Height = height;
                Stride = stride;
            }

            private static ReadOnlySpan<byte> FileSignature => "SMTF"u8;

            public int Width { get; }

            public int Height { get; }

            public int Stride { get; }

            [SkipLocalsInit]
            public void Save(Stream stream)
            {
                stream.Write(FileSignature);

                WriteInt32(stream, version);
                WriteInt32(stream, Width);
                WriteInt32(stream, Height);
                WriteInt32(stream, Stride);
            }

            [SkipLocalsInit]
            private static int ReadInt32(Stream stream)
            {
                Span<byte> bytes = stackalloc byte[4];

                stream.ReadExactly(bytes);

                return BinaryPrimitives.ReadInt32LittleEndian(bytes);
            }

            [SkipLocalsInit]
            private static void WriteInt32(Stream stream, int value)
            {
                Span<byte> bytes = stackalloc byte[4];

                BinaryPrimitives.WriteInt32LittleEndian(bytes, value);

                stream.Write(bytes);
            }
        }
    }
}
