using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Preprocessing;

public static class ImageCodec
{
    public static byte[] EncodeJpeg(Mat image, int quality = 92)
    {
        Cv2.ImEncode(".jpg", image, out var bytes, [new ImageEncodingParam(ImwriteFlags.JpegQuality, quality)]);
        return bytes;
    }

    public static Mat Decode(byte[] bytes) => Cv2.ImDecode(bytes, ImreadModes.Color);
}
