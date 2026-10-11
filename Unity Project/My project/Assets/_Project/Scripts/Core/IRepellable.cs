using UnityEngine;

namespace LanternKeeper
{
    /// <summary>
    /// Anything the lantern flare can push away. Step 3's ghost implements this; Step 2 only
    /// ships <see cref="TestDummy"/>. Put the implementing component on (or above) a collider
    /// that sits on a layer included in LanternController's "Ghost Layers" mask.
    /// Repel is called repeatedly (about 5x per second) for as long as the flare covers the object.
    /// </summary>
    public interface IRepellable
    {
        /// <param name="sourcePosition">World position of the flaring lantern.</param>
        void Repel(Vector3 sourcePosition);
    }
}
