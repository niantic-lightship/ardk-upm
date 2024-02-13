// Copyright 2022-2024 Niantic.

using UnityEngine;
using UnityEngine.XR.ARFoundation;
using JetBrains.Annotations;
using Niantic.Lightship.AR.Utilities.Logging;

namespace Niantic.Lightship.AR.Settings
{
    /// <summary>
    /// This class contains all the data required for data management requests.
    /// </summary>
    [PublicAPI]
    public static partial class PrivacyData
    {
        // using this to avoid re-subscribing to event in case of domain reloads.
        private static bool s_missingUserWarningRegistered = false;

        /// <summary>
        /// This is the device Id used to identify any device. In case there is no userId, the clientId can be provided
        /// for your GDPR data requests.
        /// If you are a Lightship developer, clientId is Unity's SystemInfo.deviceUniqueIdentifier.
        ///
        /// For your game users, it is a random Guid. In case of no userId, you have to record it.
        /// It changes if the ios/android app is uninstalled and reinstalled. It remains the same over app upgrades
        /// </summary>
        [PublicAPI]
        public static string ClientId
        {
            get => Metadata.ClientId;
        }

        /// <summary>
        /// This is the user Id that identifies each individual end user so that their data can be identified.
        /// </summary>
        [PublicAPI]
        public static string UserId
        {
            get => Metadata.UserId;
            set => SetUserId(value);
        }

        /// <summary>
        /// Sets the userId of the user when the user logs in.
        /// </summary>
        /// <param name="userId">the userId of the user </param>
        [PublicAPI]
        public static void SetUserId(string userId)
        {
            Metadata.SetUserId(userId);
        }

        /// <summary>
        /// Clears the userId when the user logs out so that we can dissociate the data from that user.
        /// </summary>
        [PublicAPI]
        public static void ClearUserId()
        {
            Metadata.ClearUserId();
        }

#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED

        [RuntimeInitializeOnLoadMethod]
        private static void RegisterMissingUserIdWarning()
        {
            if (!s_missingUserWarningRegistered)
            {
                ARSession.stateChanged += WarnUserOnMissingUserId;
            }

            s_missingUserWarningRegistered = true;
        }

        private static void WarnUserOnMissingUserId(ARSessionStateChangedEventArgs sessionStateChangedEventArgs)
        {
            if (sessionStateChangedEventArgs.state == ARSessionState.CheckingAvailability ||
                sessionStateChangedEventArgs.state == ARSessionState.Ready)
            {
                if (string.IsNullOrWhiteSpace(Metadata.UserId))
                {
                    Log.Warning($"UserId is missing. Please set a unique Id using {nameof(PrivacyData)}.{nameof(UserId)} for data management purposes. (Please see documentation for more details)");
                }

                ARSession.stateChanged -= WarnUserOnMissingUserId;
            }
        }
# endif

    }
}
