// Copyright 2022-2024 Niantic.
using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Mapping;
using Niantic.Lightship.AR.Subsystems;
using Niantic.Lightship.AR.XRSubsystems;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Subsystems.PersistentAnchor
{
    /// <summary>
    /// The Lightship implementation of the <c>XRPersistentAnchorSubsystem</c>. Do not create this directly.
    /// Use the <c>SubsystemManager</c> instead.
    /// </summary>
    [Preserve]
    public sealed class LightshipPersistentAnchorSubsystem : XRPersistentAnchorSubsystem, ISubsystemWithMutableApi<IApi>
    {
        internal class LightshipProvider : Provider
        {
            private IApi _api;
            private XRPersistentAnchorConfiguration _currentConfiguration = new XRPersistentAnchorConfiguration();

            /// <summary>
            /// The handle to the native version of the provider
            /// </summary>
            private IntPtr _nativeProviderHandle;

            private bool _isMock;
            public override bool IsMockProvider => _isMock;

            /// <summary>
            /// Construct the implementation provider.
            /// </summary>
            public LightshipProvider() : this(new NativeApi()) { }

            public LightshipProvider(IApi api)
            {
                Log.Info("LightshipPersistentAnchorSubsystem.LightshipProvider construct");
                _api = api;
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
                _nativeProviderHandle = _api.Construct(LightshipUnityContext.UnityContextHandle);
#endif
                Log.Info("LightshipPersistentAnchorSubsystem got _nativeProviderHandle: " + _nativeProviderHandle);
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
                _isMock = _api is not NativeApi;

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
                _nativeProviderHandle = IntPtr.Zero; ;
            }

            [Obsolete]
            public void Configure(IntPtr persistentAnchorApiHandle)
            {
                // TODO: Remove
            }

            public override TrackableChanges<XRPersistentAnchor> GetChanges
            (
                XRPersistentAnchor defaultAnchor,
                Allocator allocator
            )
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return default;
                }

                var changesHandle = _api.AcquireLatestChanges
                (
                    _nativeProviderHandle,
                    out IntPtr addedPtr, out int addedCount,
                    out IntPtr updatedPtr, out int updatedCount,
                    out IntPtr removedPtr, out int removedCount
                );
                try
                {
                    unsafe
                    {
                        var trackablesAddedArray = new NativeArray<XRPersistentAnchor>(addedCount, Allocator.Temp);
                        var trackablesUpdatedArray = new NativeArray<XRPersistentAnchor>(updatedCount, Allocator.Temp);
                        var trackablesRemovedArray = new NativeArray<TrackableId>(removedCount, Allocator.Temp);

                        var addedIntPtrNativeArray = NativeCopyUtility.PtrToNativeArrayWithDefault(IntPtr.Zero,
                            addedPtr.ToPointer(), sizeof(IntPtr),
                            addedCount, Allocator.Temp);

                        var updatedIntPtrNativeArray = NativeCopyUtility.PtrToNativeArrayWithDefault(IntPtr.Zero,
                            updatedPtr.ToPointer(), sizeof(IntPtr),
                            updatedCount, Allocator.Temp);

                        var removedIntPtrNativeArray = NativeCopyUtility.PtrToNativeArrayWithDefault(IntPtr.Zero,
                            removedPtr.ToPointer(), sizeof(IntPtr),
                            removedCount, Allocator.Temp);

                        for (int i = 0; i < addedCount; i++)
                        {
                            trackablesAddedArray[i] = CreateXRPersistentAnchor(addedIntPtrNativeArray[i]);
                        }

                        for (int i = 0; i < updatedCount; i++)
                        {
                            trackablesUpdatedArray[i] = CreateXRPersistentAnchor(updatedIntPtrNativeArray[i]);
                        }
                        for (int i = 0; i < removedCount; i++)
                        {
                            var xrPersistentAnchor = CreateXRPersistentAnchor(removedIntPtrNativeArray[i]);
                            trackablesRemovedArray[i] = xrPersistentAnchor.trackableId;
                        }

                        return TrackableChanges<XRPersistentAnchor>.CopyFrom(trackablesAddedArray,
                            trackablesUpdatedArray,
                            trackablesRemovedArray, Allocator.Persistent);
                    }
                }
                finally
                {
                    if (!changesHandle.IsValidHandle())
                    {
                        Log.Error("Tried to release anchor changes handle with invalid pointer.");
                    }

                    _api.ReleaseLatestChanges(changesHandle);
                }
            }

            public override bool GetNetworkStatusUpdate(out XRPersistentAnchorNetworkRequestStatus[] statuses)
            {
                statuses = default;

                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return false;
                }

                var handle = _api.AcquireNetworkStatus(_nativeProviderHandle, out var statusList, out var listCount);

                if (listCount == 0)
                {
                    if (!handle.IsValidHandle())
                    {
                        Log.Error("Tried to release network status handle with invalid pointer.");
                    }

                    _api.ReleaseNetworkStatus(handle);
                    return false;
                }

                try
                {
                    statuses = new XRPersistentAnchorNetworkRequestStatus[listCount];
                    NativeArray<IntPtr> statusPtrList;
                    unsafe
                    {
                        statusPtrList = NativeCopyUtility.PtrToNativeArrayWithDefault
                        (
                            IntPtr.Zero,
                            statusList.ToPointer(),
                            sizeof(IntPtr),
                            listCount,
                            Allocator.Temp
                        );
                    }

                    for (int i = 0; i < listCount; i++)
                    {
                        statuses[i] = GetNetworkStatus(statusPtrList[i]);
                    }
                }
                finally
                {
                    if (!handle.IsValidHandle())
                    {
                        Log.Error("Tried to release network status handle with invalid pointer.");
                    }

                    _api.ReleaseNetworkStatus(handle);
                }

                return true;
            }

            public override bool GetLocalizationStatusUpdate(out XRPersistentAnchorLocalizationStatus[] statuses)
            {
                statuses = default;

                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return false;
                }

                var handle = _api.AcquireLocalizationStatus(_nativeProviderHandle, out var statusList, out var listCount);

                if (listCount == 0)
                {
                    if (!handle.IsValidHandle())
                    {
                        Log.Error("Tried to release localization status handle with invalid pointer.");
                    }

                    _api.ReleaseLocalizationStatus(handle);
                    return false;
                }

                try
                {
                    statuses = new XRPersistentAnchorLocalizationStatus[listCount];
                    NativeArray<IntPtr> statusPtrList;
                    unsafe
                    {
                        statusPtrList = NativeCopyUtility.PtrToNativeArrayWithDefault
                        (
                            IntPtr.Zero,
                            statusList.ToPointer(),
                            sizeof(IntPtr),
                            listCount,
                            Allocator.Temp
                        );
                    }

                    for (int i = 0; i < listCount; i++)
                    {
                        statuses[i] = GetLocalizationStatus(statusPtrList[i]);
                    }
                }
                finally
                {
                    if (!handle.IsValidHandle())
                    {
                        Log.Error("Tried to release localization status handle with invalid pointer.");
                    }

                    _api.ReleaseLocalizationStatus(handle);
                }

                return true;

            }

            public override bool GetFrameDiagnosticsUpdate(out XRPersistentAnchorFrameDiagnostics[] diagnosticsArray)
            {
                diagnosticsArray = default;

                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return false;
                }

                var handle = _api.AcquireFrameDiagnostics(_nativeProviderHandle, out var diagnostics, out var listCount);

                if (listCount == 0)
                {
                    if (!handle.IsValidHandle())
                    {
                        Log.Error("Tried to release localization status handle with invalid pointer.");
                    }

                    _api.ReleaseFrameDiagnostics(handle);
                    return false;
                }

                try
                {
                    diagnosticsArray = new XRPersistentAnchorFrameDiagnostics[listCount];
                    NativeArray<IntPtr> diagnosticsPtrList;
                    unsafe
                    {
                        diagnosticsPtrList = NativeCopyUtility.PtrToNativeArrayWithDefault
                        (
                            IntPtr.Zero,
                            diagnostics.ToPointer(),
                            sizeof(IntPtr),
                            listCount,
                            Allocator.Temp
                        );
                    }

                    for (int i = 0; i < listCount; i++)
                    {
                        diagnosticsArray[i] = GetFrameDiagnostics(diagnosticsPtrList[i]);
                    }
                }
                finally
                {
                    if (!handle.IsValidHandle())
                    {
                        Log.Error("Tried to release localization status handle with invalid pointer.");
                    }

                    _api.ReleaseFrameDiagnostics(handle);
                }

                return true;

            }

            public override bool GetVpsSessionId(out string vpsSessionId)
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    vpsSessionId = default;
                    return false;
                }

                return _api.GetVpsSessionId(_nativeProviderHandle, out vpsSessionId);

            }

            public override bool TryAddAnchor(Pose pose, out XRPersistentAnchor anchor)
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    anchor = XRPersistentAnchor.defaultValue;
                    return false;
                }

                // Native TryCreateAnchor is a void function but Subsystem/Provider level function follows
                // ARF pattern to return boolean
                _api.TryCreateAnchor(_nativeProviderHandle, pose, out var anchorId);
                anchor = new XRPersistentAnchor(anchorId);
                return true;
            }

            public override bool TryRemoveAnchor(TrackableId anchorId)
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return false;
                }

                return _api.TryRemoveAnchor(_nativeProviderHandle, anchorId);
            }

            public override bool TryRestoreAnchor
            (
                XRPersistentAnchorPayload anchorPayload,
                out XRPersistentAnchor anchor
            )
            {
                return TryLocalize(anchorPayload, out anchor);
            }

            public override bool TryLocalize(XRPersistentAnchorPayload anchorPayload, out XRPersistentAnchor anchor)
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    anchor = XRPersistentAnchor.defaultValue;
                    return false;
                }

                if (_api.TryTrackAnchor(_nativeProviderHandle, anchorPayload.nativePtr, anchorPayload.size, out var anchorId))
                {
                    anchor = new XRPersistentAnchor(anchorId);
                    return true;
                }

                anchor = XRPersistentAnchor.defaultValue;
                return false;
            }

            private XRPersistentAnchor CreateXRPersistentAnchor(IntPtr anchorChangeIntPtr)
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return XRPersistentAnchor.defaultValue;
                }

                if (!anchorChangeIntPtr.IsValidHandle())
                {
                    Log.Error("Tried to extract anchor changes with invalid anchor change pointer.");
                }

                var success =
                    _api.TryExtractAnchorChange
                    (
                        anchorChangeIntPtr,
                        out var trackableId,
                        out var pose,
                        out int trackingState, out int trackingStateReason, out float trackingConfidence,
                        out var xrPersistentAnchorPayloadIntPtr, out int payloadSize,
                        out UInt64 timestampMs
                    );

                if (success)
                {
                    var xrPersistentAnchorPayload =
                        new XRPersistentAnchorPayload(xrPersistentAnchorPayloadIntPtr, payloadSize);

                    var xrPersistentAnchor =
                        new XRPersistentAnchor
                        (
                            trackableId,
                            pose,
                            (TrackingState)trackingState,
                            (TrackingStateReason)trackingStateReason,
                            xrPersistentAnchorPayload,
                            timestampMs,
                            trackingConfidence
                        );

                    return xrPersistentAnchor;
                }

                Log.Error($"Failed to create XR Persistent Anchor.");
                return default;
            }

            private XRPersistentAnchorNetworkRequestStatus GetNetworkStatus(IntPtr statusIntPtr)
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return default;
                }

                if (!statusIntPtr.IsValidHandle())
                {
                    Log.Error("Tried to extract network status with invalid status pointer.");
                }

                var success = _api.TryExtractNetworkStatus
                (
                    statusIntPtr,
                    out var requestId,
                    out var status,
                    out var type,
                    out var error,
                    out var startTime,
                    out var endTime,
                    out var frameId
                );

                if (!success)
                {
                    Log.Error("Failed to extract network status");
                    return default;
                }

                return new XRPersistentAnchorNetworkRequestStatus
                {
                    RequestId = requestId,
                    Status = (RequestStatus)status,
                    Type = (RequestType)type,
                    Error = (ErrorCode)error,
                    StartTimeMs = startTime,
                    EndTimeMs = endTime,
                    FrameId = frameId
                };
            }

            private XRPersistentAnchorLocalizationStatus GetLocalizationStatus(IntPtr statusIntPtr)
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return default;
                }

                if (!statusIntPtr.IsValidHandle())
                {
                    Log.Error("Tried to extract localization status with invalid status pointer.");
                }

                var success = _api.TryExtractLocalizationStatus
                (
                    statusIntPtr,
                    out var requestId,
                    out var status,
                    out var confidence,
                    out var frameId
                );

                if (!success)
                {
                    Log.Error("Failed to extract localization status");
                    return default;
                }

                return new XRPersistentAnchorLocalizationStatus
                {
                    NodeId = requestId,
                    Status = status,
                    LocalizationConfidence = confidence,
                    FrameId = frameId
                };
            }

            private XRPersistentAnchorFrameDiagnostics GetFrameDiagnostics(IntPtr diagnosticsIntPtr)
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return default;
                }

                if (!diagnosticsIntPtr.IsValidHandle())
                {
                    Log.Error("Tried to extract frame diagnostics with invalid status pointer.");
                }

                var success = _api.TryExtractFrameDiagnostics
                (
                    diagnosticsIntPtr,
                    out var frameId,
                    out var timestampMs,
                    out var labelNameList,
                    out var labelScoreList,
                    out var labelCount
                );

                if (!success)
                {
                    Log.Error("Failed to extract frame diagnostics");
                    return default;
                }

                // Read label names and scores
                NativeArray<UInt32> labelNames;
                NativeArray<float> labelScores;
                unsafe
                {
                    labelNames = NativeCopyUtility.PtrToNativeArrayWithDefault
                    (
                        (UInt32)0,
                        labelNameList.ToPointer(),
                        sizeof(UInt32),
                        (int)labelCount,
                        Allocator.Temp
                    );

                    labelScores = NativeCopyUtility.PtrToNativeArrayWithDefault
                    (
                        (float)0,
                        labelScoreList.ToPointer(),
                        sizeof(float),
                        (int)labelCount,
                        Allocator.Temp
                    );
                }

                Dictionary<DiagnosticLabel, float> scoresPerDiagnosticLabel = new();
                for (int i = 0; i < (int)labelCount; i++)
                {
                    scoresPerDiagnosticLabel[(DiagnosticLabel)labelNames[i]] = labelScores[i];
                }

                return new XRPersistentAnchorFrameDiagnostics
                {
                    FrameId = frameId,
                    TimestampMs = timestampMs,
                    ScoresPerDiagnosticLabel = scoresPerDiagnosticLabel
                };
            }

            public override XRPersistentAnchorConfiguration CurrentConfiguration
            {
                get => _currentConfiguration;
                set
                {
                    _currentConfiguration = value;
                    if (running)
                    {
                        Log.Warning("Configuration changed while running, stop and restart the " +
                            "PersistentAnchorSubsystem to use the new configuration");
                    }

                    var enableLearnedFeatures =
                        _currentConfiguration.DeviceMappingType == DeviceMappingType.CpuLearnedFeatures ||
                        _currentConfiguration.DeviceMappingType == DeviceMappingType.GpuLearnedFeatures;
                    var useCpuLearnedFeatures =
                        _currentConfiguration.DeviceMappingType == DeviceMappingType.CpuLearnedFeatures;

                    _api.Configure
                    (
                        _nativeProviderHandle,
                        _currentConfiguration.ContinuousLocalizationEnabled,
                        _currentConfiguration.TemporalFusionEnabled,
                        _currentConfiguration.TransformUpdateSmoothingEnabled,
                        _currentConfiguration.CloudLocalizationEnabled,
                        _currentConfiguration.DeviceMappingLocalizationEnabled,
                        enableLearnedFeatures,
                        useCpuLearnedFeatures,
                        _currentConfiguration.CloudLocalizerInitialRequestsPerSecond,
                        _currentConfiguration.CloudLocalizerContinuousRequestsPerSecond,
                        _currentConfiguration.DeviceMappingLocalizationFps,
                        _currentConfiguration.CloudLocalizationTemporalFusionWindowSize,
                        _currentConfiguration.DeviceMappingLocalizationTemporalFusionWindowSize,
                        _currentConfiguration.DiagnosticsEnabled,
                        _currentConfiguration.LimitedLocalizationsOnly,
                        _currentConfiguration.JpegCompressionQuality
                    );
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterDescriptor()
        {
            var cinfo = new XRPersistentAnchorSubsystemDescriptor.Cinfo
            {
                id = "Lightship-PersistentAnchor",
                providerType = typeof(LightshipProvider),
                subsystemTypeOverride = typeof(LightshipPersistentAnchorSubsystem),
                supportsTrackableAttachments = true
            };

            XRPersistentAnchorSubsystemDescriptor.Create(cinfo);
        }

        void ISubsystemWithMutableApi<IApi>.SwitchApiImplementation(IApi api)
        {
            ((LightshipProvider)provider).SwitchApiImplementation(api);
        }

        void ISubsystemWithMutableApi<IApi>.SwitchToInternalMockImplementation()
        {
            ((LightshipProvider)provider).SwitchApiImplementation(new MockApi());
        }
    }
}
