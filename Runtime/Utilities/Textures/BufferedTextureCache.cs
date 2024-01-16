// Copyright 2022-2024 Niantic.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Niantic.Lightship.AR.Utilities.Textures
{
    // Note: Not thread safe
    internal class BufferedTextureCache: IDisposable
    {
        protected long _activeTexture = 0;
        protected uint _currentFrameId = 0;
        protected readonly int _numBuffers;
        protected readonly Texture2D[] _textureBuffer;

        public BufferedTextureCache(int numBuffers)
        {
            _numBuffers = numBuffers;
            _textureBuffer = new Texture2D [_numBuffers];
        }

        public virtual void Dispose()
        {
            for (int i = 0; i < _numBuffers; i++)
            {
                Object.Destroy(_textureBuffer[i]);
                _textureBuffer[i] = null;
            }
        }

        public Texture2D GetActiveTexture()
        {
            return _textureBuffer[_activeTexture];
        }

        public Texture2D GetUpdatedTextureFromBuffer
        (
            IntPtr buffer,
            int size,
            int width,
            int height,
            TextureFormat format,
            uint frameId
        )
        {
            if (_currentFrameId == frameId && _textureBuffer[_activeTexture])
            {
                return _textureBuffer[_activeTexture];
            }

            PrepareTexture(width, height, format, frameId);

            _textureBuffer[_activeTexture].LoadRawTextureData(buffer, size);
            _textureBuffer[_activeTexture].Apply();
            return _textureBuffer[_activeTexture];
        }

        protected void PrepareTexture(int width, int height, TextureFormat format, uint frameId)
        {
            _currentFrameId = frameId;
            _activeTexture = (_activeTexture + 1) % _numBuffers;
            if (_textureBuffer[_activeTexture] == null
                || _textureBuffer[_activeTexture].width != width
                || _textureBuffer[_activeTexture].height != height
                || _textureBuffer[_activeTexture].format != format)
            {
                if (_textureBuffer[_activeTexture] == null)
                {
                    _textureBuffer[_activeTexture] = new Texture2D(width, height, format, false);
                }
                else
                {
                    _textureBuffer[_activeTexture].Reinitialize(width, height, format, false);
                }
            }
        }
    }
}
