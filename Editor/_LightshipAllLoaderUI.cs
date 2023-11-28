// Copyright 2022-2023 Niantic.
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;

namespace Niantic.Lightship.AR.Editor
{
    [XRCustomLoaderUI("Niantic.Lightship.AR.Loader.LightshipARCoreLoader", BuildTargetGroup.Android)]
    [XRCustomLoaderUI("Niantic.Lightship.AR.Loader.LightshipARKitLoader", BuildTargetGroup.iOS)]
    [XRCustomLoaderUI("Niantic.Lightship.AR.Loader.LightshipStandaloneLoader", BuildTargetGroup.Standalone)]
    internal class _LightshipAllLoaderUI : IXRCustomLoaderUI
    {
        struct Content
        {
            public static readonly GUIContent AndroidLoaderName = new GUIContent("Niantic Lightship SDK + Google ARCore");
            public static readonly GUIContent IOSLoaderName = new GUIContent("Niantic Lightship SDK + Apple ARKit");
            public static readonly GUIContent StandaloneLoaderName = new GUIContent("Niantic Lightship SDK for Unity Editor");

            public static GUIContent GetLoaderName(BuildTargetGroup buildTargetGroup)
            {
                switch (buildTargetGroup)
                {
                    case BuildTargetGroup.Android:
                        return AndroidLoaderName;
                    case BuildTargetGroup.iOS:
                        return IOSLoaderName;
                    default:
                        return StandaloneLoaderName;
                }
            }
        }

        public void SetRenderedLineHeight(float height)
        {
            RequiredRenderHeight = height;
        }

        public void OnGUI(Rect rect)
        {
            GUIContent loaderName = Content.GetLoaderName(ActiveBuildTargetGroup);
            var size = EditorStyles.toggle.CalcSize(loaderName);
            var labelRect = new Rect(rect);
            labelRect.width = size.x;
            labelRect.height = RequiredRenderHeight;
            IsLoaderEnabled = EditorGUI.ToggleLeft(labelRect, loaderName, IsLoaderEnabled);
        }

        public bool IsLoaderEnabled { get; set; }

        /**
         * List of incompatible Loaders which will be disabled when LightshipSDK is enabled in XR-Plugin Management.
         * Strongly typed loader references using typeof() is preferred, but any package
         * in XR-Plugin Management/Editor/Metadata/KnownPackages.cs must be referenced using hard strings.
         */
        public string[] IncompatibleLoaders => new string[]
        {
            typeof(UnityEngine.XR.ARCore.ARCoreLoader).FullName, typeof(UnityEngine.XR.ARKit.ARKitLoader).FullName,
            typeof(UnityEngine.XR.Simulation.SimulationLoader).FullName, "Unity.XR.Oculus.OculusLoader",
            "UnityEngine.XR.OpenXR.OpenXRLoader", "Unity.XR.MockHMD.MockHMDLoader", "UnityEngine.XR.WindowsMR.WindowsMRLoader",
            "UnityEngine.XR.MagicLeap.MagicLeapLoader", "Niantic.Lightship.AR.Loader.LightshipSimulationLoader"
        };

        public float RequiredRenderHeight { get; private set; }
        public BuildTargetGroup ActiveBuildTargetGroup { get; set; }
    }
}
