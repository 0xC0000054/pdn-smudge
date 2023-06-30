/////////////////////////////////////////////////////////////////////////////////
//
// ABR FileType Plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (c) 2012-2020, 2022, 2023 Nicholas Hayes
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using pyrochild.effects.smudge.Abr.Internal;
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;
using PaintDotNet;
using pyrochild.effects.common;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Drawing;
using System.Buffers;

namespace pyrochild.effects.smudge.Abr
{
    internal static class AbrBrushReader
    {
        public static List<SmudgeBrush> Load(string path, string cacheFolder)
        {
            List<SmudgeBrush> brushes;

            using (FileStream stream = new(path, FileMode.Open, FileAccess.Read))
            using (BigEndianBinaryReader reader = new(stream))
            {
                short version = reader.ReadInt16();

                switch (version)
                {
                    case 1:
                    case 2:
                        brushes = DecodeVersion1(reader, version, cacheFolder, Path.GetFileNameWithoutExtension(path));
                        break;
                    case 6:
                    case 7: // Used by Photoshop CS and later for brushes containing 16-bit data.
                    case 10: // Used by Photoshop CS6 and/or CC?
                        brushes = DecodeVersion6(reader, version, cacheFolder);
                        break;
                    default:
                        throw new FormatException(string.Format(CultureInfo.CurrentCulture, "Unsupported ABR major version: {0}", version));
                }
            }

            return brushes;
        }

        private enum AbrBrushType : short
        {
            Computed = 1,
            Sampled
        }

        private enum AbrImageCompression
        {
            Raw = 0,
            RLE = 1
        }

        private static List<SmudgeBrush> DecodeVersion1(BigEndianBinaryReader reader, short version, string cacheFolder, string abrFileName)
        {
            short count = reader.ReadInt16();

            List<SmudgeBrush> brushes = new(count);

            for (int i = 0; i < count; i++)
            {
                AbrBrushType type = (AbrBrushType)reader.ReadInt16();
                int size = reader.ReadInt32();

                long endOffset = reader.Position + size;

#if DEBUG
                System.Diagnostics.Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Brush: {0}, type: {1}, size: {2} bytes", i, type, size));
#endif
                if (type == AbrBrushType.Computed)
                {
#if DEBUG
                    // Skip the obsolete 'miscellaneous' field
                    reader.Position += 4L;
                    short spacing = reader.ReadInt16();

                    string name = string.Empty;
                    if (version == 2)
                    {
                        name = reader.ReadUnicodeString();
                    }

                    short diameter = reader.ReadInt16();
                    short roundness = reader.ReadInt16();
                    short angle = reader.ReadInt16();
                    short hardness = reader.ReadInt16();
#else
                    reader.Position += size;
#endif
                }
                else if (type == AbrBrushType.Sampled)
                {
                    // Skip the obsolete 'miscellaneous' field
                    reader.Position += 4L;
                    short spacing = reader.ReadInt16();

                    string name = string.Empty;
                    if (version == 2)
                    {
                        name = reader.ReadUnicodeString();
                    }

                    if (string.IsNullOrEmpty(name))
                    {
                        name = $"{abrFileName} brush {i}";
                    }

                    bool antiAlias = reader.ReadByte() != 0;

                    // Skip the Int16 bounds.
                    reader.Position += 8L;

                    Rectangle bounds = reader.ReadInt32Rectangle();
                    if (bounds.Width <= 0 || bounds.Height <= 0)
                    {
                        // Skip any brushes that have invalid dimensions.
                        reader.Position += (endOffset - reader.Position);
                        continue;
                    }

                    short depth = reader.ReadInt16();

                    if (depth != 8)
                    {
                        // The format specs state that brushes must be 8-bit, skip any that are not.
                        reader.Position += (endOffset - reader.Position);
                        continue;
                    }
                    int height = bounds.Height;
                    int width = bounds.Width;

                    int rowsRemaining = height;
                    int rowsRead = 0;

                    int alphaDataSize = checked(width * height);

                    byte[] pooledArray = ArrayPool<byte>.Shared.Rent(alphaDataSize);

                    try
                    {
                        Span<byte> alphaData = new Span<byte>(pooledArray, 0, alphaDataSize);

                        do
                        {
                            // Sampled brush data is broken into repeating chunks for brushes taller that 16384 pixels.
                            int chunkHeight = Math.Min(rowsRemaining, 16384);
                            // The format specs state that compression is stored as a 2-byte field, but it is written as a 1-byte field in actual files.
                            AbrImageCompression compression = (AbrImageCompression)reader.ReadByte();

                            if (compression == AbrImageCompression.RLE)
                            {
                                // Skip the compressed row lengths
                                reader.Position += (long)chunkHeight * sizeof(short);

                                for (int y = 0; y < chunkHeight; y++)
                                {
                                    int row = rowsRead + y;
                                    RLEHelper.DecodedRow(reader, alphaData.Slice(row * width, width));
                                }
                            }
                            else
                            {
                                int numBytesToRead = chunkHeight * width;
                                int numBytesRead = rowsRead * width;

                                reader.ProperRead(alphaData.Slice(numBytesRead, numBytesToRead));
                            }

                            rowsRemaining -= 16384;
                            rowsRead += 16384;

                        } while (rowsRemaining > 0);

                        Surface brush = CreateSampledBrush(width, height, depth, alphaData);

                        brushes.Add(new SmudgeBrush(name, brush, cacheFolder));
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(pooledArray);
                    }
                }
                else
                {
                    // Skip any unknown brush types.
                    reader.Position += size;
                }
            }

            return brushes;
        }

        private static List<SmudgeBrush> DecodeVersion6(BigEndianBinaryReader reader, short majorVersion, string cacheFolder)
        {
            short minorVersion = reader.ReadInt16();
            long unusedDataLength;

            switch (minorVersion)
            {
                case 1:
                    // Skip the Int16 bounds rectangle and the unknown Int16.
                    unusedDataLength = 10L;
                    break;
                case 2:
                    // Skip the unknown bytes.
                    unusedDataLength = 264L;
                    break;
                default:
                    throw new FormatException(string.Format(CultureInfo.CurrentCulture, "Unsupported ABR version: {0}.{1}.", majorVersion, minorVersion));
            }

            BrushSectionParser parser = new(reader);

            List<SmudgeBrush> brushes = new(parser.SampledBrushes.Count);

            long sampleSectionOffset = parser.SampleSectionOffset;

            if (parser.SampledBrushes.Count > 0 && sampleSectionOffset >= 0)
            {
                reader.Position = sampleSectionOffset;

                uint sectionLength = reader.ReadUInt32();

                long sectionEnd = reader.Position + sectionLength;

                while (reader.Position < sectionEnd)
                {
                    uint brushLength = reader.ReadUInt32();

                    // The brush data is padded to 4 byte alignment.
                    long paddedBrushLength = ((long)brushLength + 3) & ~3;

                    long endOffset = reader.Position + paddedBrushLength;

                    string tag = reader.ReadPascalString();

                    // Skip the unneeded data that comes before the Int32 bounds rectangle.
                    reader.Position += unusedDataLength;

                    Rectangle bounds = reader.ReadInt32Rectangle();
                    if (bounds.Width <= 0 || bounds.Height <= 0)
                    {
                        // Skip any brushes that have invalid dimensions.
                        reader.Position += (endOffset - reader.Position);
                        continue;
                    }

                    short depth = reader.ReadInt16();
                    if (depth != 8 && depth != 16)
                    {
                        // Skip any brushes with an unknown bit depth.
                        reader.Position += (endOffset - reader.Position);
                        continue;
                    }

                    SampledBrush sampledBrush = parser.SampledBrushes.FindLargestBrush(tag);
                    if (sampledBrush != null)
                    {
                        AbrImageCompression compression = (AbrImageCompression)reader.ReadByte();

                        int height = bounds.Height;
                        int width = bounds.Width;
                        Surface brush = null;

                        int alphaDataSize = depth == 16 ? checked(width * height * 2) : checked(width * height);

                        byte[] pooledArray = ArrayPool<byte>.Shared.Rent(alphaDataSize);
                        try
                        {
                            Span<byte> alphaData = new(pooledArray, 0, alphaDataSize);

                            if (compression == AbrImageCompression.RLE)
                            {
                                // Skip the compressed row lengths
                                reader.Position += (long)height * sizeof(short);

                                int bytesPerRow = depth == 16 ? checked(width * 2) : width;

                                for (int y = 0; y < height; y++)
                                {
                                    RLEHelper.DecodedRow(reader, alphaData.Slice(y * bytesPerRow, bytesPerRow));
                                }
                            }
                            else
                            {
                                reader.ProperRead(alphaData);
                            }

                            brush = CreateSampledBrush(width, height, depth, alphaData);
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(pooledArray);
                        }

                        brushes.Add(new SmudgeBrush(sampledBrush.Name, brush, cacheFolder));

                        // Some brushes only store the largest item and scale it down.
                        IEnumerable<SampledBrush> scaledBrushes = parser.SampledBrushes.Where(i => i.Tag.Equals(tag, StringComparison.Ordinal) && i.Diameter < sampledBrush.Diameter);
                        if (scaledBrushes.Any())
                        {
                            int originalWidth = brush.Width;
                            int originalHeight = brush.Height;

                            foreach (SampledBrush item in scaledBrushes.OrderByDescending(p => p.Diameter))
                            {
                                Size size = ComputeBrushSize(originalWidth, originalHeight, item.Diameter);

                                Surface scaledBrush = new(size.Width, size.Height);
                                scaledBrush.FitSurface(ResamplingAlgorithm.AdaptiveHighQuality, brush);

                                brushes.Add(new SmudgeBrush(item.Name, scaledBrush, cacheFolder));
                            }
                        }
                    }

                    long remaining = endOffset - reader.Position;
                    // Skip any remaining bytes until the next sampled brush.
                    if (remaining > 0)
                    {
                        reader.Position += remaining;
                    }
                }
            }

            return brushes;
        }

        private static unsafe Surface CreateSampledBrush(int width,
                                                         int height,
                                                         int depth,
                                                         ReadOnlySpan<byte> alphaData)
        {
            Surface brush = null;
            Surface tempBrush = null;

            try
            {
                tempBrush = new Surface(width, height);
                tempBrush.Clear();

                fixed (byte* ptr = alphaData)
                {
                    if (depth == 16)
                    {
                        int srcStride = width * 2;
                        for (int y = 0; y < height; y++)
                        {
                            byte* src = ptr + (y * srcStride);
                            ColorBgra* dst = tempBrush.GetRowPointerUnchecked(y);

                            for (int x = 0; x < width; x++)
                            {
                                ushort value = ReadUInt16BigEndian(src);

                                dst->A = SixteenBitConversion.GetEightBitValue(value);

                                src += 2;
                                dst++;
                            }
                        }
                    }
                    else
                    {
                        RegionPtr<byte> src = new(ptr, width, height, width);
                        RegionPtr<ColorBgra32> dst = new(tempBrush,
                                                         (ColorBgra32*)tempBrush.Scan0.VoidStar,
                                                         width,
                                                         height,
                                                         tempBrush.Stride);

                        PixelKernels.ReplaceChannel(dst, src, 3);
                    }
                }

                brush = tempBrush;
                tempBrush = null;
            }
            finally
            {
                if (tempBrush != null)
                {
                    tempBrush.Dispose();
                    tempBrush = null;
                }
            }

            return brush;

            static unsafe ushort ReadUInt16BigEndian(byte* ptr)
            {
                ushort value = Unsafe.ReadUnaligned<ushort>(ptr);

                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
            }
        }

        private static Size ComputeBrushSize(int originalWidth, int originalHeight, int maxEdgeLength)
        {
            Size thumbSize = Size.Empty;

            if (originalWidth <= 0 || originalHeight <= 0)
            {
                thumbSize.Width = 1;
                thumbSize.Height = 1;
            }
            else if (originalWidth > originalHeight)
            {
                int longSide = Math.Min(originalWidth, maxEdgeLength);
                thumbSize.Width = longSide;
                thumbSize.Height = Math.Max(1, (originalHeight * longSide) / originalWidth);
            }
            else if (originalHeight > originalWidth)
            {
                int longSide = Math.Min(originalHeight, maxEdgeLength);
                thumbSize.Width = Math.Max(1, (originalWidth * longSide) / originalHeight);
                thumbSize.Height = longSide;
            }
            else
            {
                int longSide = Math.Min(originalWidth, maxEdgeLength);
                thumbSize.Width = longSide;
                thumbSize.Height = longSide;
            }

            return thumbSize;
        }

        private static class SixteenBitConversion
        {
            private static readonly ImmutableArray<byte> EightBitLookupTable = CreateEightBitLookupTable();

            public static byte GetEightBitValue(ushort value)
            {
                // The 16-bit brush data is stored in the range of [0, 32768].
                // Because an unsigned value can never be negative we only need to clamp
                // to the upper bound of the lookup table.
                return EightBitLookupTable[Math.Min((int)value, 32768)];
            }

            private static ImmutableArray<byte> CreateEightBitLookupTable()
            {
                ImmutableArray<byte>.Builder builder = ImmutableArray.CreateBuilder<byte>(32769);

                for (int i = 0; i < builder.Capacity; i++)
                {
                    // The 16-bit brush data is stored in the range of [0, 32768].
                    builder.Add((byte)((i * 10) / 1285));
                }

                return builder.MoveToImmutable();
            }
        }
    }
}
