// Copyright 2023-2024 Niantic.

using System;

using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.XRSubsystems;

using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.SubsystemsImplementation;


namespace Niantic.Lightship.AR.Subsystems.WorldPositioning
{
    /// <summary>
    /// The Lightship implementation of the <c>XRWorldPositioningSubsystem</c>. Do not create this directly.
    /// Use the <c>SubsystemManager</c> instead.
    /// </summary>
    [Preserve]
    public sealed class LightshipWorldPositioningSubsystem : XRWorldPositioningSubsystem
    {
        internal class LightshipProvider : Provider
        {
            private IApi _api;

            /// <summary>
            /// The handle to the native version of the provider
            /// </summary>
            private IntPtr _nativeProviderHandle;

            /// <summary>
            /// Construct the implementation provider.
            /// </summary>
            public LightshipProvider()
                : this(new NativeApi())
            {
            }

            public LightshipProvider(IApi api)
            {
                _api = api;
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
                _nativeProviderHandle = _api.Construct(LightshipUnityContext.UnityContextHandle);
#endif
            }

            // Destruct the native provider and replace it with the provided (or default mock) provider
            // Used for testing and mocking
            public void SwitchApiImplementation(IApi api)
            {
                if (_nativeProviderHandle != IntPtr.Zero)
                {
                    _api.Stop(_nativeProviderHandle);
                    _api.Destruct(_nativeProviderHandle);
                }

                _api = api;
                _nativeProviderHandle = _api.Construct(LightshipUnityContext.UnityContextHandle);
            }

            public override void Start()
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return;
                }

                _api.Start(_nativeProviderHandle);
            }

            public override void Stop()
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return;
                }

                _api.Stop(_nativeProviderHandle);
            }

            public override void Destroy()
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return;
                }

                _api.Destruct(_nativeProviderHandle);
                _nativeProviderHandle = IntPtr.Zero;
                ;
            }

            public void Configure(IntPtr wpsApiHandle)
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return;
                }

                _api.Configure(wpsApiHandle);
            }

            public override WorldPositioningStatus TryGetXRToWorld
            (
                ref Matrix4x4 arToWorld,
                ref double originLatitude,
                ref double originLongitude,
                ref double originAltitude
            )
            {
                if (!running)
                {
                    return WorldPositioningStatus.SubsystemNotRunning;
                }

                if (!_nativeProviderHandle.IsValidHandle())
                {
                    arToWorld = Matrix4x4.zero;
                    originLatitude = 0.0;
                    originLongitude = 0.0;
                    originAltitude = 0.0;
                    return WorldPositioningStatus.Initializing;
                }

                return (WorldPositioningStatus)_api.TryGetXRToWorld
                (
                    _nativeProviderHandle,
                    out arToWorld,
                    out originLatitude,
                    out originLongitude,
                    out originAltitude
                );
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterDescriptor()
        {
            var cinfo = new XRWorldPositioningSubsystemCinfo()
            {
                id = "Lightship-WorldPositioning",
                providerType = typeof(LightshipProvider),
                subsystemTypeOverride = typeof(LightshipWorldPositioningSubsystem),
            };

            SubsystemDescriptorStore.RegisterDescriptor
                (XRWorldPositioningSubsystemDescriptor.Create(cinfo));
        }

        void SwitchApiImplementation(IApi api)
        {
            ((LightshipProvider)provider).SwitchApiImplementation(api);
        }

        void SwitchToInternalMockImplementation()
        {
            ((LightshipProvider)provider).SwitchApiImplementation(new MockApi());
        }
    }
}
