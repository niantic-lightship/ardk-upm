// Copyright 2023 Niantic, Inc. All Rights Reserved.
using Niantic.Lightship.AR.Loader;

namespace Niantic.Lightship.AR
{
    internal interface ILightshipSettingsUser
    {
        public void SetLightshipSettings(LightshipSettings settings);
    }
}
