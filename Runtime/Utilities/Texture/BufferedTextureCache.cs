// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Niantic.Lightship.AR.Utilities
{
    // BufferedTextureCache which creates all required textures and caches pointers on initialization.
    // Textures are non-readable (on CPU) because modifying pixel data using Unity's Texture APIs will
    // invalidate the cached pointers.
    //
    // Note:
    //  Texture2D.LoadImage/LoadRawTextureData() modify the underlying native texture pointer,
    //  so we must create a temporary texture first, then copy it to the desired texture with Graphics.CopyTexture().
    //  Graphics.CopyTexture() preserves the original texture pointer, so it can be cached and reused.
    //  This is less performance optimal, but it avoids the issue (UUM-3768) where calling GetNativeTexturePtr
    //  at a high frequency/framerate can run into a race condition which makes Unity freeze.
    internal class BufferedTextureCache: IDisposable
    {
        private long _activeTexture = 0;
        private uint _currentFrameId = 0;
        private bool _texturesInitialized;
        private bool _indexInitialized;
        private readonly int _numBuffers;

        private readonly Texture2D[] _textureBuffer;
        private readonly IntPtr[] _nativeTexturePtrBuffer;
        private readonly Texture2D[] _stagingTextureBuffer;

        private Texture2D _encodedTexHolder = new Texture2D(2, 2);

        public BufferedTextureCache(int numBuffers, int width, int height, TextureFormat format, bool linear)
        {
            _numBuffers = numBuffers;
            _textureBuffer = new Texture2D[_numBuffers];
            _stagingTextureBuffer = new Texture2D[_numBuffers];
            _nativeTexturePtrBuffer = new IntPtr[_numBuffers];

            InitializeTextures(width, height, format, linear);
        }

        public BufferedTextureCache(int numBuffers)
        {
            _numBuffers = numBuffers;
            _textureBuffer = new Texture2D[_numBuffers];
            _stagingTextureBuffer = new Texture2D[_numBuffers];
            _nativeTexturePtrBuffer = new IntPtr[_numBuffers];
        }

        private void InitializeTextures(int width, int height, TextureFormat format, bool linear)
        {
            for (int i = 0; i < _numBuffers; i++)
            {
                var tex = new Texture2D(width, height, format, false, linear);
                tex.Apply(false, true);

                _textureBuffer[i] = tex;
                _stagingTextureBuffer[i] = new Texture2D(width, height, format, false, linear);
                _nativeTexturePtrBuffer[i] = tex.GetNativeTexturePtr();
            }

            _texturesInitialized = true;
        }

        public Texture2D GetUpdatedTextureFromBuffer
        (
            IntPtr buffer,
            int size,
            int width,
            int height,
            TextureFormat format,
            uint frameId,
            out IntPtr nativeTexturePtr
        )
        {
            if (!_texturesInitialized)
            {
                InitializeTextures(width, height, format, false);
            }

            if (_currentFrameId == frameId && _indexInitialized)
            {
                nativeTexturePtr = _nativeTexturePtrBuffer[_activeTexture];
                return _textureBuffer[_activeTexture];
            }

            ProgressActiveIndex(frameId);

            var stagingTex = _stagingTextureBuffer[_activeTexture];
            stagingTex.LoadRawTextureData(buffer, size);
            stagingTex.Apply();

            Graphics.CopyTexture(stagingTex, _textureBuffer[_activeTexture]);
            nativeTexturePtr = _nativeTexturePtrBuffer[_activeTexture];

            return _textureBuffer[_activeTexture];
        }

        public Texture2D GetUpdatedTextureFromEncodedBuffer
        (
            byte[] encodedBytes,
            uint frameId,
            bool mirrorX,
            int stride,
            out IntPtr nativeTexturePtr
        )
        {
            if (_currentFrameId == frameId && _indexInitialized)
            {
                nativeTexturePtr = _nativeTexturePtrBuffer[_activeTexture];
                return _textureBuffer[_activeTexture];
            }

            ProgressActiveIndex(frameId);

            var stagingTex = _stagingTextureBuffer[_activeTexture];

            // Must use LoadImage to decode jpg image bytes
            _encodedTexHolder.LoadImage(encodedBytes);

            if (mirrorX)
            {
                stagingTex.LoadMirroredOverXAxis(_encodedTexHolder, stride);
            }
            else
            {
                stagingTex.LoadRawTextureData(_encodedTexHolder.GetRawTextureData<byte>());
            }

            stagingTex.Apply();

            Graphics.CopyTexture(stagingTex, _textureBuffer[_activeTexture]);
            nativeTexturePtr = _nativeTexturePtrBuffer[_activeTexture];

            return _textureBuffer[_activeTexture];
        }

        public Texture2D GetUpdatedTextureFromPath
        (
            string dataFilePath,
            uint frameId,
            out IntPtr nativeTexturePtr
        )
        {
            if (_currentFrameId == frameId && _indexInitialized)
            {
                nativeTexturePtr = _nativeTexturePtrBuffer[_activeTexture];
                return _textureBuffer[_activeTexture];
            }

            ProgressActiveIndex(frameId);

            byte[] buffer = File.ReadAllBytes(dataFilePath);

            var stagingTex = _stagingTextureBuffer[_activeTexture];
            stagingTex.LoadRawTextureData(buffer);
            stagingTex.Apply();

            Graphics.CopyTexture(stagingTex, _textureBuffer[_activeTexture]);
            nativeTexturePtr = _nativeTexturePtrBuffer[_activeTexture];

            return _textureBuffer[_activeTexture];
        }

        public void Dispose()
        {
            for (int i = 0; i < _numBuffers; i++)
            {
                Object.Destroy(_textureBuffer[i]);
                Object.Destroy(_stagingTextureBuffer[i]);
                _textureBuffer[i] = null;
                _stagingTextureBuffer[i] = null;
                _nativeTexturePtrBuffer[i] = IntPtr.Zero;
            }

            Object.Destroy(_encodedTexHolder);
        }

        private void ProgressActiveIndex(uint frameId)
        {
            _indexInitialized = true;
            _currentFrameId = frameId;
            _activeTexture = (_activeTexture + 1) % _numBuffers;
        }
    }
}
