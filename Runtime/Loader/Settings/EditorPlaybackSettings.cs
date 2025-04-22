// Copyright 2022-2025 Niantic.

using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Niantic.Lightship.AR.Loader
{
    public class EditorPlaybackSettings: ILightshipPlaybackSettings
    {
        private const string k_UsePlaybackKey = "Niantic.Lightship.AR.Settings.UsePlayback";
        private const string k_PlaybackDatasetPathKey = "Niantic.Lightship.AR.Settings.PlaybackDatasetPath";
        private const string k_RunManually = "Niantic.Lightship.AR.Settings.RunManually";
        private const string k_LoopInfinitely = "Niantic.Lightship.AR.Settings.LoopInfinitely";
        private const string k_NumIterations = "Niantic.Lightship.AR.Settings.NumIterations";
        private const string k_StartFrame = "Niantic.Lightship.AR.Settings.StartFrame";
        private const string k_EndFrame = "Niantic.Lightship.AR.Settings.EndFrame";

#if !UNITY_EDITOR
        public bool UsePlayback { get; set; }
        public string PlaybackDatasetPath { get; set; }
        public bool RunManually { get; set; }
        public bool LoopInfinitely { get; set; }
        public uint NumberOfIterations { get; set; }
        public int StartFrame { get; set; }
        public int EndFrame { get; set; }
#else
        public bool UsePlayback
        {
            get
            {
                return EditorPrefs.GetBool(k_UsePlaybackKey, false);
            }
            set
            {
                EditorPrefs.SetBool(k_UsePlaybackKey, value);
            }
        }

        public string PlaybackDatasetPath
        {
            get
            {
                return EditorPrefs.GetString(k_PlaybackDatasetPathKey);
            }
            set
            {
                EditorPrefs.SetString(k_PlaybackDatasetPathKey, value);
            }
        }

        public bool RunManually
        {
            get
            {
                return EditorPrefs.GetBool(k_RunManually);
            }
            set
            {
                EditorPrefs.SetBool(k_RunManually, value);
            }
        }

        public bool LoopInfinitely
        {
            get
            {
                return EditorPrefs.GetBool(k_LoopInfinitely);
            }
            set
            {
                EditorPrefs.SetBool(k_LoopInfinitely, value);
            }
        }
        public uint NumberOfIterations
        {
            get
            {
                return (uint) EditorPrefs.GetInt(k_NumIterations, 1);
            }
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException();
                }

                EditorPrefs.SetInt(k_NumIterations, (int)value);
            }
        }

        public int StartFrame
        {
            get
            {
                return EditorPrefs.GetInt(k_StartFrame, 0);
            }
            set
            {
                EditorPrefs.SetInt(k_StartFrame, value);
            }
        }

        public int EndFrame
        {
            get
            {
                return EditorPrefs.GetInt(k_EndFrame, -1);
            }
            set
            {
                EditorPrefs.SetInt(k_EndFrame, value);
            }
        }
#endif
    }
}
