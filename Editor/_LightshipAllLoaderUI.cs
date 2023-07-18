using UnityEditor;
using UnityEditor.XR.Management;
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
            public static readonly GUIContent k_LoaderName = new GUIContent("Niantic Lightship SDK");
        }

        public void SetRenderedLineHeight(float height)
        {
            RequiredRenderHeight = height;
        }

        public void OnGUI(Rect rect)
        {
            var size = EditorStyles.toggle.CalcSize(Content.k_LoaderName);
            var labelRect = new Rect(rect);
            labelRect.width = size.x;
            labelRect.height = RequiredRenderHeight;
            IsLoaderEnabled = EditorGUI.ToggleLeft(labelRect, Content.k_LoaderName, IsLoaderEnabled);
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
            "UnityEngine.XR.MagicLeap.MagicLeapLoader"
        };
 
        public float RequiredRenderHeight { get; private set; }
        public BuildTargetGroup ActiveBuildTargetGroup { get; set; }
    }
}
