// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using System.Text;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Niantic.Lightship.AR.Semantics
{
    /// <summary>
    /// A structure for camera-related information pertaining to a particular frame.
    /// This is used to communicate information in the <see cref="ARSemanticSegmentationManager.frameReceived" /> event.
    /// </summary>
    [PublicAPI]
    public readonly struct ARSemanticSegmentationFrameEventArgs
    {
    }
}
