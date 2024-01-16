// Copyright 2022-2024 Niantic.
using System;

namespace Niantic.Lightship.AR.PAM
{
    internal static class DataFormatConstants
    {
        public const UInt32 FlatMatrix3x3Length = 9;
        public const UInt32 FlatMatrix4x4Length = 16;

        public const int Rgba_256_144_ImgWidth = 256;
        public const int Rgba_256_144_ImgHeight = 144;
        public const UInt32 Rgba_256_144_DataLength = Rgba_256_144_ImgWidth * Rgba_256_144_ImgHeight * 4;
        
        public const int Rgb_256_256_ImgWidth = 256;
        public const int Rgb_256_256_ImgHeight = 256;
        public const UInt32 Rgb_256_256_DataLength = Rgb_256_256_ImgWidth * Rgb_256_256_ImgHeight * 3;

        public const int Jpeg_720_540_ImgWidth = 720;
        public const int Jpeg_720_540_ImgHeight = 540;
        public const int JpegQuality = 90;
        public const UInt32 Jpeg_720_540_MaxJpegDataLength = Jpeg_720_540_ImgWidth * Jpeg_720_540_ImgHeight * 12;
    }
}
