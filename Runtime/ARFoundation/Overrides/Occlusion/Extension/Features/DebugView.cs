// Copyright 2022-2025 Niantic.

using Niantic.Lightship.AR.Common;
using UnityEngine;

namespace Niantic.Lightship.AR.Occlusion.Features
{
    internal sealed class DebugView : RenderComponent
    {
        // When using shader branching
        protected override string Keyword
        {
            get => "FEATURE_DEBUG";
        }

        // When using color masking
        private const int DepthView = 5;   // RGBA: 0101
        private const int UVView = 11;     // RGBA: 1011
        private const int AllView = 15;    // RGBA: 1111


        protected override void OnMaterialAttach(Material mat)
        {
            base.OnMaterialAttach(mat);
            mat.SetFloat(ShaderProperties.ColorMaskId, DepthView);
        }

        protected override void OnMaterialDetach(Material mat)
        {
            base.OnMaterialDetach(mat);
            mat.SetFloat(ShaderProperties.ColorMaskId, 0);
        }
    }
}
