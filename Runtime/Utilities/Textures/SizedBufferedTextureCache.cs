// Copyright 2022-2024 Niantic.

using System.IO;
using UnityEngine;

namespace Niantic.Lightship.AR.Utilities.Textures
{
    internal class SizedBufferedTextureCache: BufferedTextureCache
    {
        private int _width;
        private int _height;
        private TextureFormat _format;

        public SizedBufferedTextureCache
        (
            int numBuffers,
            int width,
            int height,
            TextureFormat format,
            bool linear
        ): base(numBuffers)
        {
            _width = width;
            _height = height;
            _format = format;
        }

        public override void Dispose()
        {
            base.Dispose();
            Object.Destroy(_encodedTexHolder);
        }

        public Texture2D GetUpdatedTextureFromPath
        (
            string dataFilePath,
            uint frameId
        )
        {
            if (_currentFrameId == frameId && _textureBuffer[_activeTexture])
            {
                return _textureBuffer[_activeTexture];
            }

            PrepareTexture(_width, _height, _format, frameId);

            byte[] buffer = File.ReadAllBytes(dataFilePath);
            _textureBuffer[_activeTexture].LoadRawTextureData(buffer);
            _textureBuffer[_activeTexture].Apply();
            return _textureBuffer[_activeTexture];
        }

        private Texture2D _encodedTexHolder = new Texture2D(2, 2);

        // Note: The mirrorX param will only apply when the texture is updated.
        // Else it will simply return the cached image.
        public Texture2D GetUpdatedTextureFromEncodedBuffer
        (
            byte[] encodedBytes,
            uint frameId,
            bool mirrorX = false,
            int stride = 4
        )
        {
            if (_currentFrameId == frameId && _textureBuffer[_activeTexture])
            {
                return _textureBuffer[_activeTexture];
            }

            // Must use LoadImage to decode jpg image bytes
            _encodedTexHolder.LoadImage(encodedBytes);

            PrepareTexture(_width, _height, _format, frameId);
            if (mirrorX)
            {
                _textureBuffer[_activeTexture].LoadMirroredOverXAxis(_encodedTexHolder, stride);
            }
            else
            {
                _textureBuffer[_activeTexture].LoadRawTextureData(_encodedTexHolder.GetRawTextureData<byte>());
            }

            _textureBuffer[_activeTexture].Apply();
            return _textureBuffer[_activeTexture];
        }
    }
}
