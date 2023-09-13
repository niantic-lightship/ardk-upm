// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using System.ComponentModel;

namespace Niantic.Lightship.AR.Utilities.Preloading
{
    /// Interface for the ARDK class that pre-downloads necessary neural network model files for
    ///   awareness features. If the files are not preloaded, they will take time to download when an
    ///   AR session configured to use those features is run.
    /// Each awareness feature has one or more modes on a performance-to-quality curve, and each mode corresponds to a
    ///   different model file. See Feature.cs in Niantic.Lightship.AR.Utilities.Preloading.
    public abstract class IModelPreloader: IDisposable
    {
        /// Begins downloading the requested model file to the cache if not already present. The request status can be
        ///   polled with CurrentProgress or ExistsInCache.
        /// @returns
        ///   Returns a PreloaderStatusCode indicating the status of the request. The final result of the request may be
        ///   deferred due to asynchronous network operations, so if this request returns Success or RequestInProgress,
        ///   the user should query CurrentProgress and ExistsInCache to confirm that the network request completes
        ///   successfully.
        public abstract PreloaderStatusCode DownloadModel(DepthMode depthMode);
        public abstract PreloaderStatusCode DownloadModel(SemanticsMode semanticsMode);

        /// Register a local neural network model file for a specific feature. This overrides Lightship's URI for the
        ///   specified feature mode and remains in effect until Lightship is deinitialized. The file must be in a
        ///   location accessible by the application and remain in place for the duration of the AR session.
        /// @returns
        ///   Returns a PreloaderStatusCode indicating the result of the request.
        public abstract PreloaderStatusCode RegisterModel(DepthMode depthMode, string filepath);
        public abstract PreloaderStatusCode RegisterModel(SemanticsMode semanticsMode, string filepath);

        /// @returns
        ///   A value in the range of [0.0, 1.0] representing how much progress has been made downloading the model
        ///   file of the specified feature mode. A progress of 1.0 means the file is present in the cache. The download
        ///   progress value is server dependent and may not always support incremental progress updates.
        public abstract float CurrentProgress(DepthMode depthMode);
        public abstract float CurrentProgress(SemanticsMode semanticsMode);

        /// @returns
        ///   True if the specified feature was found in the application's cache.
        public abstract bool ExistsInCache(DepthMode depthMode);
        public abstract bool ExistsInCache(SemanticsMode semanticsMode);

        /// Clears this model file from the application's cache.
        ///   This function will fail if the download is currently in progress or if the file is not present in the
        ///   cache.
        ///   Calling this while the specified model is currently being loaded into a Lightship AR session is invalid
        ///   and will result in undefined behavior.
        /// @returns
        ///   True if the specified feature was present in the application's cache and was successfully removed.
        public abstract bool ClearFromCache(DepthMode depthMode);
        public abstract bool ClearFromCache(SemanticsMode semanticsMode);

        public abstract void Dispose();
    }

    /// Return status codes for the model preloader
    public enum PreloaderStatusCode
    {
        [Description("The request was started successfully")] Success = 0,
        [Description("The specified file already exists in the cache")] FileExistsInCache,
        [Description("A request for the specified file is already in progress")] RequestInProgress,

        [Description("Failure")] Failure,
        [Description("The specified file is not accessible by the program")] FileNotAccessible,
        [Description("One or more invalid arguments were provided")] InvalidArguments,
    }
}
