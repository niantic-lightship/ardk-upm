namespace Niantic.Lightship.AR.Subsystems
{
    /// <summary>
    /// The update order for <c>MonoBehaviour</c>s in Lightship.
    /// </summary>
    public static class LightshipARUpdateOrder
    {
        /// <summary>
        /// The <see cref="ARSession"/>'s update order. Should come first.
        /// </summary>
        public const int k_Session = UnityEngine.XR.ARFoundation.ARUpdateOrder.k_Session;

        /// <summary>
        /// The <see cref="ARPersistentAnchorManager"/>'s update order.
        /// Should come after the <see cref="ARSession"/>.
        /// </summary>
        public const int k_PersistentAnchorManager = k_Session + 1;

        /// <summary>
        /// The <see cref="ARPersistentAnchor"/>'s update order.
        /// Should come after the <see cref="ARPersistentAnchorManager"/>.
        /// </summary>
        public const int k_PersistentAnchor = k_PersistentAnchorManager + 1;

        /// <summary>
        /// The <see cref="ARScanningManager"/>'s update order.
        /// Should come after the <see cref="ARSession"/>.
        /// </summary>
        public const int k_ScanningManager = k_Session + 1;

        /// <summary>
        /// The <see cref="ARSemanticSegmentationManager"/>'s update order.
        /// Should come after the <see cref="ARSession"/>.
        /// </summary>
        public const int k_SemanticSegmentationManager = k_Session + 1;
    }
}
