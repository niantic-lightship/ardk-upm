using UnityEngine;

namespace Niantic.Lightship.AR.Utilities.Http
{
    /// <summary>
    ///  Interface for the OpenUrlCommand class.
    /// </summary>
    internal interface IOpenUrlCommand
    {
        void Execute(string url);
    }

    /// <summary>
    /// Class that wraps calls to Application.OpenURL().
    /// Wrapping this Unity API allows us to mock it in tests.
    /// </summary>
    internal class OpenURLCommand : IOpenUrlCommand
    {
        /// <summary>
        /// Constructor is private to control instantiation
        /// </summary>
        private OpenURLCommand() {}

        /// <summary>
        /// Singleton of this class for runtime use
        /// </summary>
        public static IOpenUrlCommand Instance { get; } = new OpenURLCommand();

        public void Execute(string url)
        {
            Application.OpenURL(url);
        }
    }
}
