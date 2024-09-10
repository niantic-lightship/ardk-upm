// Copyright 2022-2024 Niantic.

using UnityEngine;

namespace Niantic.Lightship.AR.Loader
{
    public static class LightshipSettingsHelper
    {
        // The values in this settings instance can be different from the values
        // currently used by the Lightship system, if they have been altered since
        // components in the Lightship system have been initialized.
        private static RuntimeLightshipSettings _activeSettings;

        // This is guaranteed to stay the same object between when either of this class's
        // create methods are called, and when ClearRuntimeSettings is called. That means
        // that users can safely cache this object and always get the latest values.
        public static RuntimeLightshipSettings ActiveSettings
        {
            get
            {
                return _activeSettings;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void CreateRuntimeSettingsFromAsset()
        {
            var asset = LightshipSettings.Instance;
            _activeSettings = new RuntimeLightshipSettings(asset);
        }

        public static void CreateDefaultRuntimeSettings()
        {
            _activeSettings = new RuntimeLightshipSettings();
        }

        // Used to make sure settings are cleared between test case runs.
        // In a non-test setting, this cannot be called because there's no
        // master exit point for Unity applications, and instead we rely on the
        // ActiveSettings to be re-initialized when the application is restarted.
        public static void ClearRuntimeSettings()
        {
            _activeSettings = null;
        }
    }
}
