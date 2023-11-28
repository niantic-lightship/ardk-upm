// Copyright 2022-2023 Niantic.

using System;
using System.Diagnostics;
using UnityEngine;

namespace Niantic.Lightship.AR.Utilities.Log
{
    internal static class Log
    {
        private static Logger _arLog;
        private static Logger ARLog
        {
            get
            {
                if (null == _arLog)
                {
                    _arLog = new Logger(UnityEngine.Debug.unityLogger.logHandler);

                    // Reduce the default logging level but allow the LogLevel property to override it after instantiation.
                    _arLog.filterLogType = LogType.Error;

                    // Disable ARDK logs based on Unity's current build mode, but allow the LogLevel property
                    // to enable them after instantiation.
                    _arLog.logEnabled = UnityEngine.Debug.isDebugBuild;
                }

                return _arLog;
            }
        }

        /// <summary>
        ///   Setting this property enables ARDK log messages in Unity and determines the lowest log level to print,
        ///   regardless of the ARDK and Unity build modes.
        /// </summary>
        public static LogType LogLevel
        {
            set
            {
                ARLog.filterLogType = value;
                ARLog.logEnabled = true;
            }
        }

        /// <summary>
        ///   Writes a message to Unity's console via the ARDK logger.
        /// </summary>
        /// <param name="message">The message to print.</param>
        public static void Info(string message)
        {
            var caller = _GetCallerFromStack(2);

            ARLog.Log(LogType.Log, caller, message);
        }

        /// <summary>
        ///   Writes a message to Unity's console via the ARDK logger.
        /// </summary>
        /// <param name="message">The message to print.</param>
        public static void Debug(string message)
        {
            var caller = _GetCallerFromStack(2);

            ARLog.Log(LogType.Log, caller, message);
        }

        /// <summary>
        ///   Writes a warning message to Unity's console via the ARDK logger.
        /// </summary>
        /// <param name="message">The message to print.</param>
        public static void Warning(string message)
        {
            var caller = _GetCallerFromStack(2);

            ARLog.Log(LogType.Warning, caller, message);
        }

        /// <summary>
        ///   Writes an error message to Unity's console via the ARDK logger.
        /// </summary>
        /// <param name="message">The message to print.</param>
        public static void Error(string message)
        {
            var caller = _GetCallerFromStack(2);

            ARLog.Log(LogType.Error, caller, message);
        }

        /// <summary>
        ///   Prints the contents of an exception and the provided context. Does not rethrow the exception
        ///   or halt execution.
        /// </summary>
        /// <param name="exception">Exception to print</param>
        /// <param name="context">Context for the exception</param>
        internal static void Exception(Exception exception, object context = null)
        {
            var caller = _GetCallerFromStack(2);

            var message = "{0} from context: {1}";
            var str = String.Format(message, exception, context);

            ARLog.Log(LogType.Exception, caller, str);
        }

        /// <summary>
        /// Gets the full name (Namespace.Class) of the method calling this.
        /// </summary>
        /// <param name="nestedLevel">Level of nested-ness of this call. For example, to get the direct
        ///   caller of this method, the level would be 1. To get the caller that calls this through
        ///   a helper, the level would be 2. </param>
        /// <returns>A string with the full name of the calling class, or "Niantic.Lightship" if reflection fails</returns>
        private static string _GetCallerFromStack(int nestedLevel)
        {
            // Get the frame above the current one (the caller of this method)
            var callerFrame = new StackFrame(nestedLevel, false);

            var caller = callerFrame?.GetMethod()?.ReflectedType;

            if (caller == null)
                return "Niantic.Lightship";

            return caller.FullName;
        }
    }
}
