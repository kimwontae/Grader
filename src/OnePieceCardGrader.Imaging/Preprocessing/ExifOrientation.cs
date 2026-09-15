using System.Buffers.Binary;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Preprocessing;

public static class ExifOrientation
{
    public static Mat LoadOriented(string path)
    {
        var mat = Cv2.ImRead(path, ImreadModes.Color);
        if (mat.Empty())
        {
            throw new InvalidOperationException($"이미지를 열 수 없습니다: {path}");
        }

        var orientation = ReadJpegOrientation(path);
        return Apply(mat, orientation);
    }

    public static Mat Apply(Mat source, int orientation)
    {
        if (orientation is 1 or 0)
        {
            return source;
        }

        var destination = new Mat();
        switch (orientation)
        {
            case 2:
                Cv2.Flip(source, destination, FlipMode.Y);
                break;
            case 3:
                Cv2.Rotate(source, destination, RotateFlags.Rotate180);
                break;
            case 4:
                Cv2.Flip(source, destination, FlipMode.X);
                break;
            case 5:
                using (var rotated = new Mat())
                {
                    Cv2.Rotate(source, rotated, RotateFlags.Rotate90Clockwise);
                    Cv2.Flip(rotated, destination, FlipMode.Y);
                }

                break;
            case 6:
                Cv2.Rotate(source, destination, RotateFlags.Rotate90Clockwise);
                break;
            case 7:
                using (var rotated = new Mat())
                {
                    Cv2.Rotate(source, rotated, RotateFlags.Rotate90Counterclockwise);
                    Cv2.Flip(rotated, destination, FlipMode.Y);
                }

                break;
            case 8:
                Cv2.Rotate(source, destination, RotateFlags.Rotate90Counterclockwise);
                break;
            default:
                destination.Dispose();
                return source;
        }

        source.Dispose();
        return destination;
    }

    public static int ReadJpegOrientation(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8)
            {
                return 1;
            }

            var offset = 2;
            while (offset + 4 < bytes.Length && bytes[offset] == 0xFF)
            {
                var marker = bytes[offset + 1];
                if (marker == 0xDA)
                {
                    break;
                }

                var size = (bytes[offset + 2] << 8) | bytes[offset + 3];
                if (marker == 0xE1)
                {
                    var payloadStart = offset + 4;
                    if (payloadStart + size - 2 <= bytes.Length &&
                        HasExifHeader(bytes, payloadStart))
                    {
                        return ReadOrientationFromExif(bytes, payloadStart + 6);
                    }
                }

                offset += 2 + size;
            }
        }
        catch (IOException)
        {
            return 1;
        }

        return 1;
    }

    private static bool HasExifHeader(byte[] bytes, int start) =>
        start + 6 <= bytes.Length &&
        bytes[start] == (byte)'E' &&
        bytes[start + 1] == (byte)'x' &&
        bytes[start + 2] == (byte)'i' &&
        bytes[start + 3] == (byte)'f' &&
        bytes[start + 4] == 0 &&
        bytes[start + 5] == 0;

    private static int ReadOrientationFromExif(byte[] bytes, int tiffStart)
    {
        if (tiffStart + 8 > bytes.Length)
        {
            return 1;
        }

        var littleEndian = bytes[tiffStart] == (byte)'I' && bytes[tiffStart + 1] == (byte)'I';
        var ifdOffset = ReadUInt32(bytes, tiffStart + 4, littleEndian);
        var ifdStart = tiffStart + (int)ifdOffset;
        if (ifdStart + 2 > bytes.Length)
        {
            return 1;
        }

        var entryCount = ReadUInt16(bytes, ifdStart, littleEndian);
        for (var i = 0; i < entryCount; i++)
        {
            var entry = ifdStart + 2 + (i * 12);
            if (entry + 12 > bytes.Length)
            {
                break;
            }

            var tag = ReadUInt16(bytes, entry, littleEndian);
            if (tag != 0x0112)
            {
                continue;
            }

            var value = ReadUInt16(bytes, entry + 8, littleEndian);
            return value is >= 1 and <= 8 ? value : 1;
        }

        return 1;
    }

    private static ushort ReadUInt16(byte[] bytes, int offset, bool littleEndian) =>
        littleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2))
            : BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset, 2));

    private static uint ReadUInt32(byte[] bytes, int offset, bool littleEndian) =>
        littleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4))
            : BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
}
