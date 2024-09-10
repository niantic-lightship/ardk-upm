// Copyright 2022-2024 Niantic.

using System;
using UnityEngine;

namespace Niantic.Lightship.AR.Utilities.Logging
{
    public enum LogLevel
    {
        All = 0,
        Debug,
        Info,
        Warn,
        Error,
        Off,
    }

    internal static class LogLevelHelpers
    {
        public static LogType ConvertLogLevelToUnityLogType(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.All => LogType.Log,
                LogLevel.Debug => LogType.Log,
                LogLevel.Info => LogType.Log,
                LogLevel.Warn => LogType.Warning,
                LogLevel.Error => LogType.Error,
                LogLevel.Off => LogType.Exception,
                _ => throw new NotImplementedException("invalid log level provided.")
            };
        }
    }
}
