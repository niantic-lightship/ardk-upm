using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine;

namespace Niantic.Lightship.AR.Editor
{
    [XRCustomLoaderUI("Niantic.Lightship.AR.LightshipARCoreLoader", BuildTargetGroup.Android)]
    [XRCustomLoaderUI("Niantic.Lightship.AR.LightshipARKitLoader", BuildTargetGroup.iOS)]
    [XRCustomLoaderUI("Niantic.Lightship.AR.LightshipStandaloneLoader", BuildTargetGroup.Standalone)]
    internal class _LightshipAllLoaderUI : IXRCustomLoaderUI
    {
        struct Content
        {
            public static readonly GUIContent k_LoaderName = new GUIContent("Lightship SDK");
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

        public string[] IncompatibleLoaders => new string[]
        {
            "UnityEngine.XR.ARCore.ARCoreLoader", "UnityEngine.XR.ARKit.ARKitLoader",
            "Unity.XR.Oculus.OculusLoader", "UnityEngine.XR.OpenXR.OpenXRLoader",
            "UnityEngine.XR.Simulation.SimulationLoader", "Unity.XR.MockHMD.MockHMDLoader"
        };

        public float RequiredRenderHeight { get; private set; }
        public BuildTargetGroup ActiveBuildTargetGroup { get; set; }
    }
}
