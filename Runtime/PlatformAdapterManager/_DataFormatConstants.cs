using System;

namespace Niantic.Lightship.AR.PlatformAdapterManager
{
    internal static class _DataFormatConstants
    {
        public const UInt32 FLAT_MATRIX3x3_LENGTH = 9;
        public const UInt32 FLAT_MATRIX4x4_LENGTH = 16;

        public const int RGBA_256_144_IMG_WIDTH = 256;
        public const int RGBA_256_144_IMG_HEIGHT = 144;
        public const UInt32 RGBA_256_144_DATA_LENGTH = RGBA_256_144_IMG_WIDTH * RGBA_256_144_IMG_HEIGHT * 4;

        public const int JPEG_720_540_IMG_WIDTH = 720;
        public const int JPEG_720_540_IMG_HEIGHT = 540;
        public const int JPEG_QUALITY = 90;
        public const UInt32 JPEG_720_540_MAX_JPEG_DATA_LENGTH = JPEG_720_540_IMG_WIDTH * JPEG_720_540_IMG_HEIGHT * 12;
    }
}
