using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR;
using UnityEngine;

public class LightshipMeshingProvider
{
    public LightshipMeshingProvider(IntPtr unityContext)
    {
        Lightship_ARDK_Unity_Meshing_Provider_Construct(unityContext);
    }

    [DllImport(_LightshipPlugin.Name)]
    private static extern IntPtr Lightship_ARDK_Unity_Meshing_Provider_Construct(IntPtr unityContext);
}
