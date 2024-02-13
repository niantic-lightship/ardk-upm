// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using Unity.Profiling;

namespace Niantic.Lightship.AR.Utilities.Profiling
{
    internal class UnityProfiler : IProfiler
    {
        private Dictionary<string, ProfilerMarker> _markers;
        private ProfilerMarker? _currStep;

        public bool Initialize()
        {
            _markers = new Dictionary<string, ProfilerMarker>();

            return true;
        }

        public void Shutdown()
        {
            return;
        }

        private string GetEventName(string category, string name, string step = "")
        {
            if (string.IsNullOrEmpty(step))
                return $"{category}|{name}";

            return $"{category}|{name}|{step}";
        }

        private ProfilerMarker CreateAndOrBeginMarker(string markerName)
        {
            if (!_markers.TryGetValue(markerName, out ProfilerMarker marker))
            {
                marker = new ProfilerMarker(ProfilerCategory.Scripts, markerName);
                _markers.Add(markerName, marker);
            }

            marker.Begin();
            return marker;
        }

        public void EventBegin(string category, string name)
        {
            var markerName = GetEventName(category, name);
            CreateAndOrBeginMarker(markerName);
        }

        public void EventBegin(string category, string name, string arg1_name, string arg1_val)
        {
            EventBegin(category, name);
        }

        public void EventBegin
        (
            string category,
            string name,
            string arg1_name,
            string arg1_val,
            string arg2_name,
            string arg2_val
        )
        {
            EventBegin(category, name);
        }

        public void EventEnd(string category, string name)
        {
            var markerName = GetEventName(category, name);
            if (!_markers.TryGetValue(markerName, out ProfilerMarker marker))
            {
                throw new ArgumentException($"No profiler event with category: {category} and name: {name} exists.");
            }

            if (_currStep.HasValue)
            {
                _currStep.Value.End();
                _currStep = null;
            }

            marker.End();
        }

        public void EventEnd(string category, string name, string arg1_name, string arg1_val)
        {
            EventEnd(category, name);
        }

        public void EventEnd
        (
            string category,
            string name,
            string arg1_name,
            string arg1_val,
            string arg2_name,
            string arg2_val
        )
        {
            EventEnd(category, name);
        }

        public void EventStep(string category, string name, string step)
        {
            if (_currStep.HasValue)
            {
                _currStep.Value.End();
            }

            var marker = CreateAndOrBeginMarker(GetEventName(category, name, step));
            _currStep = marker;
        }

        public void EventStep(string category, string name, string step, string arg1_name, string arg1_val)
        {
            EventStep(category, name, step);
        }

        // There's no such thing as an "instance" or "immediate" event in terms of the Unity Profiler.
        // All numbers in the Unity Profiler are durations: "how long did something take to happen." So no-op, for now.
        // TODO (AR-18105):
        //  Investigate if event "instances" (immediate start/stops) add helpful visuals to the Unity Profiler
        //  Timeline view.
        public void EventInstance(string category, string name)
        {
            // no-op
        }

        public void EventInstance(string category, string name, string arg1_name, string arg1_val)
        {
            // no-op
        }

        public void EventInstance
        (
            string category,
            string name,
            string arg1_name,
            string arg1_val,
            string arg2_name,
            string arg2_val
        )
        {
            // no-op
        }
    }
}
