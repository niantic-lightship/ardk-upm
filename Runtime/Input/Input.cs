// Copyright 2022-2024 Niantic.
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Bindings;

namespace Niantic.Lightship.AR
{
    /// For the most part, a straight pass-through wrapper of the UnityEngine.Input class.
    /// However, if AR is running using Lightship Playback, then Input.location and Input.compass will
    /// return Playback-compatible objects that return values from recorded datasets.
    public class Input
    {
        private static LocationService locationServiceInstance;
        private static Compass compassInstance;

        /// <summary>
        ///   <para>Returns the value of the virtual axis identified by axisName.</para>
        /// </summary>
        /// <param name="axisName"></param>
        public static float GetAxis(string axisName) => UnityEngine.Input.GetAxis(axisName);

        /// <summary>
        ///   <para>Returns the value of the virtual axis identified by axisName with no smoothing filtering applied.</para>
        /// </summary>
        /// <param name="axisName"></param>
        public static float GetAxisRaw(string axisName) => UnityEngine.Input.GetAxisRaw(axisName);

        /// <summary>
        ///   <para>Returns true while the virtual button identified by buttonName is held down.</para>
        /// </summary>
        /// <param name="buttonName">The name of the button such as Jump.</param>
        /// <returns>
        ///   <para>True when an axis has been pressed and not released.</para>
        /// </returns>
        public static bool GetButton(string buttonName) => UnityEngine.Input.GetButtonDown(buttonName);

        /// <summary>
        ///   <para>Returns true during the frame the user pressed down the virtual button identified by buttonName.</para>
        /// </summary>
        /// <param name="buttonName"></param>
        public static bool GetButtonDown(string buttonName) => UnityEngine.Input.GetButtonDown(buttonName);

        /// <summary>
        ///   <para>Returns true the first frame the user releases the virtual button identified by buttonName.</para>
        /// </summary>
        /// <param name="buttonName"></param>
        public static bool GetButtonUp(string buttonName) => UnityEngine.Input.GetButtonDown(buttonName);

        /// <summary>
        ///   <para>Returns whether the given mouse button is held down.</para>
        /// </summary>
        /// <param name="button"></param>
        public static bool GetMouseButton(int button) => UnityEngine.Input.GetMouseButton(button);

        /// <summary>
        ///   <para>Returns true during the frame the user pressed the given mouse button.</para>
        /// </summary>
        /// <param name="button"></param>
        public static bool GetMouseButtonDown(int button) => UnityEngine.Input.GetMouseButtonDown(button);

        /// <summary>
        ///   <para>Returns true during the frame the user releases the given mouse button.</para>
        /// </summary>
        /// <param name="button"></param>
        public static bool GetMouseButtonUp(int button) => UnityEngine.Input.GetMouseButtonUp(button);

        /// <summary>
        ///   <para>Resets all input. After ResetInputAxes all axes return to 0 and all buttons return to 0 for one frame.</para>
        /// </summary>
        public static void ResetInputAxes() => UnityEngine.Input.ResetInputAxes();

#if UNITY_STANDALONE_LINUX
    /// <summary>
    ///   <para>Determine whether a particular joystick model has been preconfigured by Unity. (Linux-only).</para>
    /// </summary>
    /// <param name="joystickName">The name of the joystick to check (returned by Input.GetJoystickNames).</param>
    /// <returns>
    ///   <para>True if the joystick layout has been preconfigured; false otherwise.</para>
    /// </returns>
    public static bool IsJoystickPreconfigured(string joystickName) => UnityEngine.Input.IsJoystickPreconfigured(joystickName);
#endif

        /// <summary>
        ///   <para>Retrieves a list of input device names corresponding to the index of an Axis configured within Input Manager.</para>
        /// </summary>
        /// <returns>
        ///   <para>Returns an array of joystick and gamepad device names.</para>
        /// </returns>
        public static string[] GetJoystickNames() => UnityEngine.Input.GetJoystickNames();

        /// <summary>
        ///   <para>Call Input.GetTouch to obtain a Touch struct.</para>
        /// </summary>
        /// <param name="index">The touch input on the device screen.</param>
        /// <returns>
        ///   <para>Touch details in the struct.</para>
        /// </returns>
        public static Touch GetTouch(int index) => UnityEngine.Input.GetTouch(index);

        /// <summary>
        ///   <para>Returns specific acceleration measurement which occurred during last frame. (Does not allocate temporary variables).</para>
        /// </summary>
        /// <param name="index"></param>
        public static AccelerationEvent GetAccelerationEvent(int index) =>
            UnityEngine.Input.GetAccelerationEvent(index);

        /// <summary>
        ///   <para>Returns true while the user holds down the key identified by the key KeyCode enum parameter.</para>
        /// </summary>
        /// <param name="key"></param>
        public static bool GetKey(KeyCode key) => UnityEngine.Input.GetKey(key);

        /// <summary>
        ///   <para>Returns true while the user holds down the key identified by name.</para>
        /// </summary>
        /// <param name="name"></param>
        public static bool GetKey(string name) => UnityEngine.Input.GetKey(name);

        /// <summary>
        ///   <para>Returns true during the frame the user releases the key identified by the key KeyCode enum parameter.</para>
        /// </summary>
        /// <param name="key"></param>
        public static bool GetKeyUp(KeyCode key) => UnityEngine.Input.GetKeyUp(key);

        /// <summary>
        ///   <para>Returns true during the frame the user releases the key identified by name.</para>
        /// </summary>
        /// <param name="name"></param>
        public static bool GetKeyUp(string name) => UnityEngine.Input.GetKeyUp(name);

        /// <summary>
        ///   <para>Returns true during the frame the user starts pressing down the key identified by the key KeyCode enum parameter.</para>
        /// </summary>
        /// <param name="key"></param>
        public static bool GetKeyDown(KeyCode key) => UnityEngine.Input.GetKeyDown(key);

        /// <summary>
        ///   <para>Returns true during the frame the user starts pressing down the key identified by name.</para>
        /// </summary>
        /// <param name="name"></param>
        public static bool GetKeyDown(string name) => UnityEngine.Input.GetKeyDown(name);

        /// <summary>
        ///   <para>Enables/Disables mouse simulation with touches. By default this option is enabled.</para>
        /// </summary>
        public static bool simulateMouseWithTouches
        {
            get => UnityEngine.Input.simulateMouseWithTouches;
            set => UnityEngine.Input.simulateMouseWithTouches = true;
        }

        /// <summary>
        ///   <para>Is any key or mouse button currently held down? (Read Only)</para>
        /// </summary>
        public static bool anyKey => UnityEngine.Input.anyKey;

        /// <summary>
        ///   <para>Returns true the first frame the user hits any key or mouse button. (Read Only)</para>
        /// </summary>
        public static bool anyKeyDown => UnityEngine.Input.anyKeyDown;

        /// <summary>
        ///   <para>Returns the keyboard input entered this frame. (Read Only)</para>
        /// </summary>
        public static string inputString => UnityEngine.Input.inputString;

        /// <summary>
        ///   <para>The current mouse position in pixel coordinates. (Read Only).</para>
        /// </summary>
        public static Vector3 mousePosition => UnityEngine.Input.mousePosition;

        /// <summary>
        ///   <para>The current mouse scroll delta. (Read Only)</para>
        /// </summary>
        public static Vector2 mouseScrollDelta => UnityEngine.Input.mouseScrollDelta;

        /// <summary>
        ///   <para>Controls enabling and disabling of IME input composition.</para>
        /// </summary>
        public static IMECompositionMode imeCompositionMode
        {
            get => UnityEngine.Input.imeCompositionMode;
            set => UnityEngine.Input.imeCompositionMode = value;
        }

        /// <summary>
        ///   <para>The current IME composition string being typed by the user.</para>
        /// </summary>
        public static string compositionString => UnityEngine.Input.compositionString;

        /// <summary>
        ///   <para>Does the user have an IME keyboard input source selected?</para>
        /// </summary>
        public static bool imeIsSelected => UnityEngine.Input.imeIsSelected;

        /// <summary>
        ///   <para>The current text input position used by IMEs to open windows.</para>
        /// </summary>
        public static Vector2 compositionCursorPos => UnityEngine.Input.compositionCursorPos;

        /// <summary>
        ///   <para>Property indicating whether keypresses are eaten by a textinput if it has focus (default true).</para>
        /// </summary>
        [Obsolete("eatKeyPressOnTextFieldFocus property is deprecated, and only provided to support legacy behavior.")]
        public static bool eatKeyPressOnTextFieldFocus
        {
            get => UnityEngine.Input.eatKeyPressOnTextFieldFocus;
            set => UnityEngine.Input.eatKeyPressOnTextFieldFocus = value;
        }

        /// <summary>
        ///   <para>Indicates if a mouse device is detected.</para>
        /// </summary>
        public static bool mousePresent => UnityEngine.Input.mousePresent;

        /// <summary>
        ///   <para>Number of touches. Guaranteed not to change throughout the frame. (Read Only)</para>
        /// </summary>
        public static int touchCount => UnityEngine.Input.touchCount;

        /// <summary>
        ///   <para>Bool value which let's users check if touch pressure is supported.</para>
        /// </summary>
        public static bool touchPressureSupported => UnityEngine.Input.touchPressureSupported;

        /// <summary>
        ///   <para>Returns true when Stylus Touch is supported by a device or platform.</para>
        /// </summary>
        public static bool stylusTouchSupported => UnityEngine.Input.stylusTouchSupported;

        /// <summary>
        ///   <para>Returns whether the device on which application is currently running supports touch input.</para>
        /// </summary>
        public static bool touchSupported => UnityEngine.Input.touchSupported;

        /// <summary>
        ///   <para>Property indicating whether the system handles multiple touches.</para>
        /// </summary>
        public static bool multiTouchEnabled
        {
            get => UnityEngine.Input.multiTouchEnabled;
            set => UnityEngine.Input.multiTouchEnabled = value;
        }

        [Obsolete("isGyroAvailable property is deprecated. Please use SystemInfo.supportsGyroscope instead.")]
        public static bool isGyroAvailable => UnityEngine.Input.isGyroAvailable;

        /// <summary>
        ///   <para>Device physical orientation as reported by OS. (Read Only)</para>
        /// </summary>
        public static DeviceOrientation deviceOrientation => UnityEngine.Input.deviceOrientation;

        /// <summary>
        ///   <para>Last measured linear acceleration of a device in three-dimensional space. (Read Only)</para>
        /// </summary>
        public static Vector3 acceleration => UnityEngine.Input.acceleration;

        /// <summary>
        ///   <para>This property controls if input sensors should be compensated for screen orientation.</para>
        /// </summary>
        public static bool compensateSensors
        {
            get => UnityEngine.Input.compensateSensors;
            set => UnityEngine.Input.compensateSensors = value;
        }

        /// <summary>
        ///   <para>Number of acceleration measurements which occurred during last frame.</para>
        /// </summary>
        public static int accelerationEventCount => UnityEngine.Input.accelerationEventCount;

        /// <summary>
        ///   <para>Should Back button quit the application?
        ///
        /// Only usable on Android, Windows Phone or Windows Tablets.</para>
        /// </summary>
        public static bool backButtonLeavesApp
        {
            get => UnityEngine.Input.backButtonLeavesApp;
            set => UnityEngine.Input.backButtonLeavesApp = value;
        }

        /// <summary>
        ///   <para>Property for accessing device location (handheld devices only). (Read Only)</para>
        /// </summary>
        public static LocationService location
        {
            get
            {
                if (locationServiceInstance == null)
                    locationServiceInstance = new LocationService();

                return locationServiceInstance;
            }
        }

        /// <summary>
        ///   <para>Property for accessing compass (handheld devices only). (Read Only)</para>
        /// </summary>
        public static Compass compass
        {
            get
            {
                if (compassInstance == null)
                    compassInstance = new Compass();

                return compassInstance;
            }
        }

        /// <summary>
        ///   <para>Returns default gyroscope.</para>
        /// </summary>
        public static Gyroscope gyro => UnityEngine.Input.gyro;

        /// <summary>
        ///   <para>Returns list of objects representing status of all touches during last frame. (Read Only) (Allocates temporary variables).</para>
        /// </summary>
        public static Touch[] touches => UnityEngine.Input.touches;

        /// <summary>
        ///   <para>Returns list of acceleration measurements which occurred during the last frame. (Read Only) (Allocates temporary variables).</para>
        /// </summary>
        public static AccelerationEvent[] accelerationEvents => UnityEngine.Input.accelerationEvents;
    }
}
