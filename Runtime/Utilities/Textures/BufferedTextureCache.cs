// Copyright 2022-2024 Niantic.

using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Niantic.Lightship.AR.Utilities.Textures
{
    /// <summary>
    /// NOT THREAD SAFE
    /// Ring buffer for storing textures.
    /// </summary>
    internal class BufferedTextureCache: IDisposable
    {
        private long _activeTextureIndex = 0;
        private uint _currentFrameId = 0;
        private readonly Texture2D[] _textureBuffer;

        public BufferedTextureCache(int cacheSize)
        {
            _textureBuffer = new Texture2D[cacheSize];
        }

        public virtual void Dispose()
        {
            for (int i = 0; i < _textureBuffer.Length; i++)
            {
                Object.Destroy(_textureBuffer[i]);
                _textureBuffer[i] = null;
            }
        }

        public Texture2D GetActiveTexture()
        {
            return _textureBuffer[_activeTextureIndex];
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
            if(IsCacheHit(frameId, out var texture))
            {
                return texture;
            }

            // else reinitialise texture with the right format
            ReinitializeTexture(width, height, format, frameId);

            _textureBuffer[_activeTextureIndex].LoadRawTextureData(buffer, size);
            _textureBuffer[_activeTextureIndex].Apply();
            return _textureBuffer[_activeTextureIndex];
        }

        /// <summary>
        /// check if the current frame Id is the same as the frameId param
        /// and if the texture for the current cache is null or not
        /// </summary>
        /// <param name="frameId">frame id</param>
        /// <param name="texture">texture to provide</param>
        /// <returns>Returns if cache hit happened or not</returns>
        private bool IsCacheHit(uint frameId, out Texture2D texture)
        {
            texture = null;
            if (_currentFrameId == frameId && _textureBuffer[_activeTextureIndex])
            {
                texture = _textureBuffer[_activeTextureIndex];
                return true;
            }

            return false;
        }

        /// <summary>
        /// Initialise or reinitialise the texture buffer with the correct dimensions and format.
        /// </summary>
        /// <param name="width">width of texture</param>
        /// <param name="height">height of texture</param>
        /// <param name="format">format of texture</param>
        /// <param name="frameId">frame Id</param>
        private void ReinitializeTexture(int width, int height, TextureFormat format, uint frameId)
        {
            _currentFrameId = frameId;
            _activeTextureIndex = (_activeTextureIndex + 1) % _textureBuffer.Length;
            if (_textureBuffer[_activeTextureIndex] == null
                || _textureBuffer[_activeTextureIndex].width != width
                || _textureBuffer[_activeTextureIndex].height != height
                || _textureBuffer[_activeTextureIndex].format != format)
            {
                if (_textureBuffer[_activeTextureIndex] == null)
                {
                    _textureBuffer[_activeTextureIndex] = new Texture2D(width, height, format, false);
                }
                else
                {
                    _textureBuffer[_activeTextureIndex].Reinitialize(width, height, format, false);
                }
            }
        }
    }
}
