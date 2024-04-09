// Copyright 2022-2024 Niantic.

using System;
using System.ComponentModel;

namespace Niantic.Lightship.AR.Utilities.Preloading
{
    /// <summary>
    /// Interface for the ARDK class that pre-downloads necessary neural network model files for
    /// awareness features. If the files are not preloaded, they will take time to download when an
    /// AR session configured to use those features is run.
    /// </summary>
    /// <remarks>
    /// Each awareness feature has one or more modes on a performance-to-quality curve, and each mode corresponds to a
    /// different model file. See <see cref="Feature"/> in <see cref="Niantic.Lightship.AR.Utilities.Preloading"/>.
    /// </remarks>
    [PublicAPI]
    public abstract class IModelPreloader: IDisposable
    {
        /// <summary>
        /// Begins downloading the requested model file to the cache if not already present. The request status can be
        /// polled with <see cref="CurrentProgress(DepthMode, out float)"/> or <see cref="ExistsInCache(DepthMode)"/>.
        /// </summary>
        /// <returns>
        /// Returns a <see cref="PreloaderStatusCode"/> indicating the initial status of the request. The final result of the request
        /// may be deferred due to asynchronous network operations, so if this request returns <see cref="PreloaderStatusCode.Success"/> or
        /// <see cref="PreloaderStatusCode.RequestInProgress"/>, the user should continue to query <see cref="CurrentProgress(DepthMode, out float)"/>
        /// every frame to confirm that the network request completes successfully.
        /// </returns>
        public abstract PreloaderStatusCode DownloadModel(DepthMode depthMode);

        /// <summary>
        /// Begins downloading the requested model file to the cache if not already present. The request status can be
        /// polled with <see cref="CurrentProgress(SemanticsMode, out float)"/> or <see cref="ExistsInCache(SemanticsMode)"/>.
        /// </summary>
        /// <returns>
        /// Returns a <see cref="PreloaderStatusCode"/> indicating the initial status of the request. The final result of the request
        /// may be deferred due to asynchronous network operations, so if this request returns <see cref="PreloaderStatusCode.Success"/> or
        /// <see cref="PreloaderStatusCode.RequestInProgress"/>, the user should continue to query <see cref="CurrentProgress(SemanticsMode, out float)"/>
        /// every frame to confirm that the network request completes successfully.
        /// </returns>
        public abstract PreloaderStatusCode DownloadModel(SemanticsMode semanticsMode);

        /// <summary>
        /// Begins downloading the requested model file to the cache if not already present. The request status can be
        /// polled with <see cref="CurrentProgress(ObjectDetectionMode, out float)"/> or <see cref="ExistsInCache(ObjectDetectionMode)"/>.
        /// </summary>
        /// <returns>
        /// Returns a <see cref="PreloaderStatusCode"/> indicating the initial status of the request. The final result of the request
        /// may be deferred due to asynchronous network operations, so if this request returns <see cref="PreloaderStatusCode.Success"/> or
        /// <see cref="PreloaderStatusCode.RequestInProgress"/>, the user should continue to query <see cref="CurrentProgress(ObjectDetectionMode, out float)"/>
        /// every frame to confirm that the network request completes successfully.
        /// </returns>
        public abstract PreloaderStatusCode DownloadModel(ObjectDetectionMode objectDetectionMode);

        /// <summary>
        /// Register a local neural network model file for a specific feature. This overrides Lightship's URI for the
        /// specified feature mode and remains in effect until Lightship is deinitialized. The file must be in a
        /// location accessible by the application and remain in place for the duration of the AR session.
        /// </summary>
        /// <returns>
        /// Returns a <see cref="PreloaderStatusCode"/> indicating the result of the request.
        /// </returns>
        public abstract PreloaderStatusCode RegisterModel(DepthMode depthMode, string filepath);

        /// <summary>
        /// Register a local neural network model file for a specific feature. This overrides Lightship's URI for the
        /// specified feature mode and remains in effect until Lightship is deinitialized. The file must be in a
        /// location accessible by the application and remain in place for the duration of the AR session.
        /// </summary>
        /// <returns>
        /// Returns a <see cref="PreloaderStatusCode"/> indicating the result of the request.
        /// </returns>
        public abstract PreloaderStatusCode RegisterModel(SemanticsMode semanticsMode, string filepath);

        /// <summary>
        /// Register a local neural network model file for a specific feature. This overrides Lightship's URI for the
        /// specified feature mode and remains in effect until Lightship is deinitialized. The file must be in a
        /// location accessible by the application and remain in place for the duration of the AR session.
        /// </summary>
        /// <returns>
        /// Returns a <see cref="PreloaderStatusCode"/> indicating the result of the request.
        /// </returns>
        public abstract PreloaderStatusCode RegisterModel(ObjectDetectionMode depthMode, string filepath);

        /// <summary>
        /// Read the current status of a mode file request, if a request was previously made. The result of
        /// DownloadModel is asynchronous, so the caller must poll <see cref="CurrentProgress(DepthMode, out float)"/>
        /// for the status of the request to ensure that the HTTP request completed successfully.
        /// </summary>
        /// <param name="progress">
        /// A value in the range of [0.0, 1.0] representing how much progress has been made downloading the model
        /// file of the specified feature mode. A progress of 1.0 means the file is present in the cache. The download
        /// progress value is server dependent and may not always support incremental progress updates.
        /// </param>
        /// <returns>
        /// Returns a <see cref="PreloaderStatusCode"/> indicating the status of the request.
        /// </returns>
        public abstract PreloaderStatusCode CurrentProgress(DepthMode depthMode, out float progress);

        /// <summary>
        /// Read the current status of a mode file request, if a request was previously made. The result of
        /// DownloadModel is asynchronous, so the caller must poll <see cref="CurrentProgress(SemanticsMode, out float)"/>
        /// for the status of the request to ensure that the HTTP request completed successfully.
        /// </summary>
        /// <param name="progress">
        /// A value in the range of [0.0, 1.0] representing how much progress has been made downloading the model
        /// file of the specified feature mode. A progress of 1.0 means the file is present in the cache. The download
        /// progress value is server dependent and may not always support incremental progress updates.
        /// </param>
        /// <returns>
        /// Returns a <c>PreloaderStatusCode</c> indicating the status of the request.
        /// </returns>
        public abstract PreloaderStatusCode CurrentProgress(SemanticsMode semanticsMode, out float progress);

        /// <summary>
        /// Read the current status of a mode file request, if a request was previously made. The result of
        /// DownloadModel is asynchronous, so the caller must poll <see cref="CurrentProgress(ObjectDetectionMode, out float)"/>
        /// for the status of the request to ensure that the HTTP request completed successfully.
        /// </summary>
        /// <param name="progress">
        /// A value in the range of [0.0, 1.0] representing how much progress has been made downloading the model
        /// file of the specified feature mode. A progress of 1.0 means the file is present in the cache. The download
        /// progress value is server dependent and may not always support incremental progress updates.
        /// </param>
        /// <returns>
        /// Returns a <see cref="PreloaderStatusCode"/> indicating the status of the request.
        /// </returns>
        public abstract PreloaderStatusCode CurrentProgress(ObjectDetectionMode depthMode, out float progress);

        /// <summary>
        /// Checks if a model associated with the specified <see cref="DepthMode"/> is present in the cache.
        /// </summary>
        /// <returns>
        /// True if the specified model was found in the application's cache.
        /// </returns>
        public abstract bool ExistsInCache(DepthMode depthMode);

        /// <summary>
        /// Checks if a model associated with the specified <see cref="SemanticsMode"/> is present in the cache.
        /// </summary>
        /// <returns>
        /// True if the specified model was found in the application's cache.
        /// </returns>
        public abstract bool ExistsInCache(SemanticsMode semanticsMode);

        /// <summary>
        /// Checks if a model associated with the specified <see cref="ObjectDetectionMode"/> is present in the cache.
        /// </summary>
        /// <returns>
        /// True if the specified model was found in the application's cache.
        /// </returns>
        public abstract bool ExistsInCache(ObjectDetectionMode depthMode);

        /// <summary>
        /// Clears this model file from the application's cache.
        /// This function will fail if the download is currently in progress or if the file is not present in the
        /// cache.
        /// Calling this while the specified model is currently being loaded into a Lightship AR session is invalid
        /// and will result in undefined behavior.
        /// </summary>
        /// <returns>
        /// True if the specified feature was present in the application's cache and was successfully removed.
        /// </returns>
        public abstract bool ClearFromCache(DepthMode depthMode);

        /// <summary>
        /// Clears this model file from the application's cache.
        /// This function will fail if the download is currently in progress or if the file is not present in the
        /// cache.
        /// Calling this while the specified model is currently being loaded into a Lightship AR session is invalid
        /// and will result in undefined behavior.
        /// </summary>
        /// <returns>
        /// True if the specified feature was present in the application's cache and was successfully removed.
        /// </returns>
        public abstract bool ClearFromCache(SemanticsMode semanticsMode);

        /// <summary>
        /// Clears this model file from the application's cache.
        /// This function will fail if the download is currently in progress or if the file is not present in the
        /// cache.
        /// Calling this while the specified model is currently being loaded into a Lightship AR session is invalid
        /// and will result in undefined behavior.
        /// </summary>
        /// <returns>
        /// True if the specified feature was present in the application's cache and was successfully removed.
        /// </returns>
        public abstract bool ClearFromCache(ObjectDetectionMode depthMode);

        /// <summary>
        /// Dispose the handle to the native model preloader.
        /// </summary>
        public abstract void Dispose();
    }

    /// <summary>
    /// Return status codes for the model preloader
    /// </summary>
    public enum PreloaderStatusCode
    {
        [Description("The request was started successfully")] Success = 0,
        [Description("The specified file already exists in the cache")] FileExistsInCache,
        [Description("A request for the specified file is already in progress")] RequestInProgress,

        [Description("Failure")] Failure,
        [Description("The specified file is not accessible by the program")] FileNotAccessible,
        [Description("A preloader request for this model file was not found")] RequestNotFound,
        [Description("One or more invalid arguments were provided")] InvalidArguments,
        [Description("An HTTP error occurred and the request failed")] HttpError,
        [Description("The HTTP request timed out")] HttpTimeout,
    }
}
