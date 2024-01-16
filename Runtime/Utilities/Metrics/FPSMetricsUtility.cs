// Copyright 2022-2024 Niantic.
using System;
using static Niantic.Lightship.AR.Utilities.Log.Log;
using Niantic.Lightship.AR.Subsystems;
using Niantic.Lightship.AR.Subsystems.Meshing;
using Niantic.Lightship.AR.Subsystems.Occlusion;
using Niantic.Lightship.AR.Subsystems.Semantics;
using Niantic.Lightship.AR.XRSubsystems;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using Object = UnityEngine.Object;

namespace Niantic.Lightship.AR.Utilities.Metrics
{
    internal class FPSMetricsUtility
    {
        private ARCameraManager _cameraManager;

        private XROcclusionSubsystem _occlusionSubsystem;
        private LightshipSemanticsSubsystem _semanticsSubsystem;
        private LightshipMeshingProvider _meshingProvider;

        private bool _automaticallyTrackFPS;
        private bool _canAutomaticallyTrackFPS;

        private bool _usingDepth;
        private bool _usingSemantics;
        private bool _usingMesh;

        private ulong _lastTimeDepth;
        private ulong _lastTimeSemantics;
        private ulong _lastTimeMesh;

        private float _instantDepthFPS;
        private float _instantSemanticsFPS;
        private float _instantMeshFPS;

        private uint? _latestSemanticsFrameId;

        public FPSMetricsUtility(
            bool usingDepth = true,
            bool usingSemantics = true,
            bool usingMesh = true,
            bool automaticallyTrackFPS = true)
        {
            _usingDepth = usingDepth;
            _usingSemantics = usingSemantics;
            _usingMesh = usingMesh;
            _automaticallyTrackFPS = automaticallyTrackFPS;

            var xrManager = XRGeneralSettings.Instance.Manager;
            if (!xrManager.isInitializationComplete)
            {
                Warning("XRManager is not initialized yet: cannot get subsystems");
                return;
            }

            _occlusionSubsystem = xrManager.activeLoader.GetLoadedSubsystem<XROcclusionSubsystem>();
            if (_occlusionSubsystem is null)
            {
                Warning("Depth FPS not being tracked");
                _usingDepth = false;
            }

            _semanticsSubsystem = xrManager.activeLoader.GetLoadedSubsystem<XRSemanticsSubsystem>() as LightshipSemanticsSubsystem;
            if (_semanticsSubsystem is null)
            {
                Warning("Semantics FPS not being tracked");
                _usingSemantics = false;
            }

            var activeMeshSubsystem = xrManager.activeLoader.GetLoadedSubsystem<XRMeshSubsystem>();
            if (activeMeshSubsystem is null || activeMeshSubsystem.SubsystemDescriptor.id != "LightshipMeshing")
            {
                Warning("Mesh FPS not being tracked");
                _usingMesh = false;
            }

            TryAutomaticallyTrackFPS();
        }

        public void Dispose()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_cameraManager != null)
            {
                _cameraManager.frameReceived -= OnFrameReceived;
            }
        }

        private void TryAutomaticallyTrackFPS()
        {
            if (_automaticallyTrackFPS)
            {
                // Check if we can actually track the fps using the ARCameraManager frameReceived event
                _cameraManager = Object.FindObjectOfType<ARCameraManager>(includeInactive: true);

                if (_cameraManager == null)
                {
                    Warning("Cannot track FPS: No ARCameraManager found in scene");
                    _canAutomaticallyTrackFPS = false;
                    SceneManager.sceneLoaded += OnSceneLoaded;
                    return;
                }

                _canAutomaticallyTrackFPS = true;
                _cameraManager.frameReceived += OnFrameReceived;

                // Check if we can actually track the depth fps using the AROcclusionManager
                var arOcclusionManager = Object.FindObjectOfType<AROcclusionManager>(includeInactive: true);
                if (arOcclusionManager == null)
                {
                    Warning("Cannot track depth FPS: No AROcclusionManager found in scene");
                    _usingDepth = false;
                    return;
                }

                if (arOcclusionManager.currentOcclusionPreferenceMode !=
                    OcclusionPreferenceMode.PreferEnvironmentOcclusion)
                {
                    Warning("Cannot track depth FPS: " +
                        "AROcclusionManager is not set to PreferEnvironmentOcclusion");
                    _usingDepth = false;
                    return;
                }

                _usingDepth = true;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            if (_cameraManager is not null)
            {
                _cameraManager.frameReceived -= OnFrameReceived;
            }
            SceneManager.sceneLoaded -= OnSceneLoaded;
            TryAutomaticallyTrackFPS();
        }

        private void OnFrameReceived(ARCameraFrameEventArgs args)
        {
            if (_usingDepth)
            {
                var thisTimeDepth = GetLatestDepthTimestamp();
                if (_usingDepth && thisTimeDepth != _lastTimeDepth)
                {
                    _instantDepthFPS = (1.0f / (Math.Abs((long)(thisTimeDepth - _lastTimeDepth)) / 1000.0f));
                    _lastTimeDepth = thisTimeDepth;
                }
            }

            if (_usingSemantics)
            {
                var thisTimeSemantics = GetLatestSemanticsTimestamp();
                if (_usingSemantics && thisTimeSemantics != _lastTimeSemantics)
                {
                    _instantSemanticsFPS = (1.0f / (Math.Abs((long)(thisTimeSemantics - _lastTimeSemantics)) / 1000.0f));
                    _lastTimeSemantics = thisTimeSemantics;
                }
            }

            if (_usingMesh)
            {
                var thisTimeMesh = GetLatestMeshTimestamp();
                if (_usingMesh && thisTimeMesh != _lastTimeMesh)
                {
                    _instantMeshFPS = (1.0f / (Math.Abs((long)(thisTimeMesh - _lastTimeMesh)) / 1000.0f));
                    _lastTimeMesh = thisTimeMesh;
                }
            }
        }

        public ulong GetLatestDepthTimestamp()
        {
            ulong depthTimestampMs = 0;

            if (_occlusionSubsystem.TryAcquireEnvironmentDepthCpuImage(out XRCpuImage depthBuffer))
            {
                depthTimestampMs = (ulong)(depthBuffer.timestamp * 1000);
                depthBuffer.Dispose();
            }

            return depthTimestampMs;
        }

        public float GetInstantDepthFPS()
        {
            if (_usingDepth && _canAutomaticallyTrackFPS)
            {
                return _instantDepthFPS;
            }

            return 0;
        }

        public ulong GetLatestSemanticsTimestamp()
        {
            ulong semanticsTimestampMs = 0;

            // If we have already acquired the latest frame, return the last timestamp
            if (_latestSemanticsFrameId == _semanticsSubsystem.LatestFrameId)
            {
                return _lastTimeSemantics;
            }

            if (_semanticsSubsystem.TryAcquirePackedSemanticChannelsCpuImage(out XRCpuImage semanticsBuffer, out Matrix4x4 _))
            {
                semanticsTimestampMs = (ulong)(semanticsBuffer.timestamp * 1000);
                _latestSemanticsFrameId = _semanticsSubsystem.LatestFrameId;
                semanticsBuffer.Dispose();
            }

            return semanticsTimestampMs;
        }

        public float GetInstantSemanticsFPS()
        {
            if (_usingSemantics && _canAutomaticallyTrackFPS)
            {
                return _instantSemanticsFPS;
            }

            return 0;
        }

        public ulong GetLatestMeshTimestamp()
        {
            return LightshipMeshingProvider.GetLastMeshUpdateTime();
        }

        public float GetInstantMeshFPS()
        {
            if (_usingMesh && _canAutomaticallyTrackFPS)
            {
                return _instantMeshFPS;
            }

            return 0;
        }
    }
}
