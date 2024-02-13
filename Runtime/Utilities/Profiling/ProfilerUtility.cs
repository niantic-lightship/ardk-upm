// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Niantic.Lightship.AR.Utilities.Profiling
{
    internal static class ProfilerUtility
    {
        private static HashSet<IProfiler> _profilers = new HashSet<IProfiler>();

        [Conditional("ENABLE_LIGHTSHIP_PROFILER")]
        public static void RegisterProfiler(IProfiler profiler)
        {
            if (!_profilers.Contains(profiler))
            {
                _profilers.Add(profiler);
                profiler.Initialize();
            }
        }

        [Conditional("ENABLE_LIGHTSHIP_PROFILER")]
        public static void ShutdownAll()
        {
            foreach (var profiler in _profilers)
            {
                profiler.Shutdown();
            }

            _profilers.Clear();
        }

        [Conditional("ENABLE_LIGHTSHIP_PROFILER")]
        public static void EventBegin(string category, string name)
        {
            foreach (var profiler in _profilers)
            {
                profiler.EventBegin(category, name);
            }
        }

        [Conditional("ENABLE_LIGHTSHIP_PROFILER")]
        public static void EventBegin(string category, string name, string arg1_name, string arg1_val)
        {
            foreach (var profiler in _profilers)
            {
                profiler.EventBegin(category, name, arg1_name, arg1_val);
            }
        }

        [Conditional("ENABLE_LIGHTSHIP_PROFILER")]
        public static void EventBegin
        (
            string category,
            string name,
            string arg1_name,
            string arg1_val,
            string arg2_name,
            string arg2_val
        )
        {
            foreach (var profiler in _profilers)
            {
                profiler.EventBegin(category, name, arg1_name, arg1_val, arg2_name, arg2_val);
            }
        }

        [Conditional("ENABLE_LIGHTSHIP_PROFILER")]
        public static void EventStep(string category, string name, string step)
        {
            foreach (var profiler in _profilers)
            {
                profiler.EventStep(category, name, step);
            }
        }

        [Conditional("ENABLE_LIGHTSHIP_PROFILER")]
        public static void EventStep
        (
            string category,
            string name,
            string step,
            string arg1_name,
            string arg1_val
        )
        {
            foreach (var profiler in _profilers)
            {
                profiler.EventStep(category, name, step, arg1_name, arg1_val);
            }
        }

        [Conditional("ENABLE_LIGHTSHIP_PROFILER")]
        public static void EventEnd(string category, string name)
        {
            foreach (var profiler in _profilers)
            {
                profiler.EventEnd(category, name);
            }
        }

        [Conditional("ENABLE_LIGHTSHIP_PROFILER")]
        public static void EventEnd
        (
            string category,
            string name,
            string arg1_name,
            string arg1_val
        )
        {
            foreach (var profiler in _profilers)
            {
                profiler.EventEnd(category, name, arg1_name, arg1_val);
            }
        }

        [Conditional("ENABLE_LIGHTSHIP_PROFILER")]
        public static void EventEnd
        (
            string category,
            string name,
            string arg1_name,
            string arg1_val,
            string arg2_name,
            string arg2_val
        )
        {
            foreach (var profiler in _profilers)
            {
                profiler.EventEnd(category, name, arg1_name, arg1_val, arg2_name, arg2_val);
            }
        }

        [Conditional("ENABLE_LIGHTSHIP_PROFILER")]
        public static void EventInstance(string category, string name, CustomProcessingOptions processingOptions = null)
        {
            if (processingOptions != null)
            {
                EventInstance
                (
                    category,
                    name,
                    CustomProcessingOptions.CUSTOM_PROCESSING_OPTIONS_KEY,
                    processingOptions.Serialize()
                );
                return;
            }

            foreach (var profiler in _profilers)
            {
                profiler.EventInstance(category, name);
            }
        }

        [Conditional("ENABLE_LIGHTSHIP_PROFILER")]
        public static void EventInstance
        (
            string category,
            string name,
            string arg1_name,
            string arg1_val,
            CustomProcessingOptions processingOptions = null
        )
        {
            if (processingOptions != null)
            {
                EventInstance
                (
                    category,
                    name,
                    arg1_name,
                    arg1_val,
                    CustomProcessingOptions.CUSTOM_PROCESSING_OPTIONS_KEY,
                    processingOptions.Serialize()
                );
                return;
            }

            foreach (var profiler in _profilers)
            {
                profiler.EventInstance(category, name, arg1_name, arg1_val);
            }
        }

        [Conditional("ENABLE_LIGHTSHIP_PROFILER")]
        public static void EventInstance
        (
            string category,
            string name,
            string arg1_name,
            string arg1_val,
            string arg2_name,
            string arg2_val
        )
        {
            foreach (var profiler in _profilers)
            {
                profiler.EventInstance(category, name, arg1_name, arg1_val, arg2_name, arg2_val);
            }
        }
    }
}
