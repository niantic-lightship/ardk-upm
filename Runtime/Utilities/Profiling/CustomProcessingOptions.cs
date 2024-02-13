using UnityEngine;


// Custom processing flags for the trace pipeline. Must match:
// argeo/ardk-next/common/profiler/custom_processing_options.h
// argeo/ardk-next/trace_pipeline/trace_pipeline/custom_processing_options.py
// LookerStudio Trace Pipeline Dashboard > Data > TEST_monitor_result > 'health_percent' & 'health_filter_name'
internal class CustomProcessingOptions
{
    public const string CUSTOM_PROCESSING_OPTIONS_KEY = "TRACE_PIPELINE_CUSTOM_PROCESSING_OPTIONS";

    public enum Type {
        NONE = 0, // No custom processing, arg value is used directly (default)
        TIME_UNTIL_NEXT = 1, // Measures the duration that passes until another identical event is thrown
        CUMULATIVE_SUM = 2 // Tracks a total value by continually adding diffs with matching arg names
    };

    public enum Filter {
        LOW_PASS = 0, // Anything below X standard deviations above the mean is considered healthy (default)
        HIGH_PASS = 1, // Anything above X standard deviations below the mean is considered healthy
        BAND_PASS = 2 // Anything within X standard deviations from the mean is considered healthy
    };

    public Type ProcessingType = Type.NONE;
    public Filter HealthFilter = Filter.LOW_PASS;

    public string Serialize()
    {
        return JsonUtility.ToJson(this);
    }
}
