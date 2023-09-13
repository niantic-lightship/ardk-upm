// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;

namespace Niantic.Lightship.Spaces
{
    public class ImageHackDisplay : MonoBehaviour
    {
        [Header("Camera Feed")] public RawImage CameraRawImage;
        private Texture2D _cameraTexture;

        void Update()
        {
            var format = TextureFormat.RGBA32;

            if (_cameraTexture == null || _cameraTexture.width != 720 || _cameraTexture.height != 540)
            {
                _cameraTexture = new Texture2D(720, 540, format, false);
            }

            var pixelPtr = SpacesCameraImageHack.GetPixelDataToDisplay();
            if (_cameraTexture && pixelPtr != IntPtr.Zero)
            {
                unsafe
                {
                    NativeArray<byte> pixelData = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(
                        (void*)pixelPtr, 720 * 540 *4, Allocator.None);
                    _cameraTexture.SetPixelData(pixelData,0,0);
                }
                
                _cameraTexture.Apply();
                CameraRawImage.texture = _cameraTexture;
            }
        }
    }
}
