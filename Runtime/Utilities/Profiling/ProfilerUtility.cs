// Copyright 2022-2024 Niantic.
//#define NIANTIC_FAST_MEMORY_PROFILER

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Niantic.Lightship.AR.Utilities.Profiling
{
    internal static class ProfilerUtility
    {
        private static HashSet<IProfiler> _profilers = new HashSet<IProfiler>();

#if NIANTIC_FAST_MEMORY_PROFILER
        private static StreamWriter profilerWriter;
        private static Dictionary<string, Dictionary<string, Stopwatch>> profilerWatch =
            new Dictionary<string, Dictionary<string, Stopwatch>>(1024 * 1024);
        private static Dictionary<string, Dictionary<string, long>> profilerTotal =
            new Dictionary<string, Dictionary<string, long>>(1024 * 1024);
        private static Dictionary<string, Dictionary<string, long>> profilerCount =
            new Dictionary<string, Dictionary<string, long>>(1024 * 1024);
        private static uint profileDumpIndex = 0;
        private static long profileDumpId = (long)(DateTime.UtcNow - new DateTime(2024, 1, 1))
            .TotalSeconds * 10;

        public static void RegisterProfiler(IProfiler profiler)
        {
            // Fast Memory Profiler uses a single memory profiler
            if (profilerWriter == null)
            {
                string fileName = string.Format("run_{0}_profile{1:D3}.txt", profileDumpId, profileDumpIndex++);
                string filePath = Path.Combine(UnityEngine.Application.persistentDataPath, fileName);
                UnityEngine.Debug.LogWarning($"DumpProfile {filePath}");

                profilerWriter = new StreamWriter(filePath);
            }
        }

        public static void ShutdownAll()
        {
            WriteProfileStatsToFile();
            profilerWriter.Close();
        }

        public static void Flush()
        {
            WriteProfileStatsToFile();
        }

        public static string GetCurrentProfilingStats()
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var keyValue in profilerTotal)
            {
                stringBuilder.AppendLine(string.Format("{0,-96} \t{2,-16} \t{1,-16}",
                    "Name", "AvgMs", "TotalMs"));

                stringBuilder.AppendLine(keyValue.Key);

                foreach (var subkeyValue in profilerTotal[keyValue.Key])
                {
                    float avgElapsedUs = subkeyValue.Value / (float)profilerCount[keyValue.Key][subkeyValue.Key];

                    string title = $"  {subkeyValue.Key}";
                    string totalMs = string.Format("{0}ms", subkeyValue.Value / 1000.0f);
                    string avgMs = string.Format("{0}ms", avgElapsedUs / 1000.0f);

                    stringBuilder.AppendLine(string.Format("{0,-96} \t{2,-16} \t{1,-16}",
                        title, avgMs, totalMs));
                }
            }

            stringBuilder.AppendLine();
            return stringBuilder.ToString();
        }

        public static void WriteProfileStatsToFile()
        {
            profilerWriter.Write(GetCurrentProfilingStats());
            profilerWriter.Flush();
        }

        public static void EventBegin(string eventKey, string eventSubkey)
        {
            if (!profilerWatch.ContainsKey(eventKey))
            {
                profilerWatch[eventKey] = new Dictionary<string, Stopwatch>(64);
                profilerTotal[eventKey] = new Dictionary<string, long>(64);
                profilerCount[eventKey] = new Dictionary<string, long>(64);
            }

            if (!profilerTotal[eventKey].ContainsKey(eventSubkey))
            {
                profilerTotal[eventKey][eventSubkey] = 0;
                profilerCount[eventKey][eventSubkey] = 0;
            }

            profilerWatch[eventKey][eventSubkey] = new Stopwatch();
            profilerWatch[eventKey][eventSubkey].Start();
        }

        public static void EventEnd(string eventKey, string eventSubkey)
        {
            profilerWatch[eventKey][eventSubkey].Stop();
            long elapsedUs = profilerWatch[eventKey][eventSubkey].ElapsedTicks / 10;
            profilerTotal[eventKey][eventSubkey] += elapsedUs;
            profilerCount[eventKey][eventSubkey]++;
        }

        public static void EventBegin(string category, string name, string unused0, string unused1)
        {
            EventBegin(category, name);
        }

        public static void EventBegin(string category, string name, string unused0, string unused1, string unused2, string unused3)
        {
            EventBegin(category, name);
        }

        public static void EventEnd(string category, string name, string unused0, string unused1)
        {
            EventEnd(category, name);
        }

        public static void EventEnd(string category, string name,
            string unused0, string unused1, string unused2, string unused3)
        {
            EventEnd(category, name);
        }
#else
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
        public static void Flush()
        {
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
        public static void EventEnd(string category, string name)
        {
            foreach (var profiler in _profilers)
            {
                profiler.EventEnd(category, name);
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
#endif

        [Conditional("ENABLE_LIGHTSHIP_PROFILER")]
        public static void EventAsyncBegin(string category, string name, ulong id)
        {
            foreach (var profiler in _profilers)
            {
                profiler.EventAsyncBegin(category, name, id);
            }
        }

        [Conditional("ENABLE_LIGHTSHIP_PROFILER")]
        public static void EventAsyncBegin
        (
            string category,
            string name,
            ulong id,
            string arg1_name,
            string arg1_val
        )
        {
            foreach (var profiler in _profilers)
            {
                profiler.EventAsyncBegin(category, name, id, arg1_name, arg1_val);
            }
        }

        [Conditional("ENABLE_LIGHTSHIP_PROFILER")]
        public static void EventAsyncBegin
        (
            string category,
            string name,
            ulong id,
            string arg1_name,
            string arg1_val,
            string arg2_name,
            string arg2_val
        )
        {
            foreach (var profiler in _profilers)
            {
                profiler.EventAsyncBegin(category, name, id, arg1_name, arg1_val, arg2_name, arg2_val);
            }
        }

        [Conditional("ENABLE_LIGHTSHIP_PROFILER")]
        public static void EventAsyncStep(string category, string name, ulong id, string step)
        {
            foreach (var profiler in _profilers)
            {
                profiler.EventAsyncStep(category, name, id, step);
            }
        }

        [Conditional("ENABLE_LIGHTSHIP_PROFILER")]
        public static void EventAsyncStep
        (
            string category,
            string name,
            ulong id,
            string step,
            string arg1_name,
            string arg1_val
        )
        {
            foreach (var profiler in _profilers)
            {
                profiler.EventAsyncStep(category, name, id, step, arg1_name, arg1_val);
            }
        }

        [Conditional("ENABLE_LIGHTSHIP_PROFILER")]
        public static void EventAsyncEnd(string category, string name, ulong id)
        {
            foreach (var profiler in _profilers)
            {
                profiler.EventAsyncEnd(category, name, id);
            }
        }

        [Conditional("ENABLE_LIGHTSHIP_PROFILER")]
        public static void EventAsyncEnd
        (
            string category,
            string name,
            ulong id,
            string arg1_name,
            string arg1_val
        )
        {
            foreach (var profiler in _profilers)
            {
                profiler.EventAsyncEnd(category, name, id, arg1_name, arg1_val);
            }
        }

        [Conditional("ENABLE_LIGHTSHIP_PROFILER")]
        public static void EventAsyncEnd
        (
            string category,
            string name,
            ulong id,
            string arg1_name,
            string arg1_val,
            string arg2_name,
            string arg2_val
        )
        {
            foreach (var profiler in _profilers)
            {
                profiler.EventAsyncEnd(category, name, id, arg1_name, arg1_val, arg2_name, arg2_val);
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
