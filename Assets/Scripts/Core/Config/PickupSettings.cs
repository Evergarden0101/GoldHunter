using System;

namespace GoldHunter.Core.Config
{
    /// <summary>Loose gold lying on the floor after a punch or a popper hit.</summary>
    [Serializable]
    public class PickupSettings
    {
        public float Radius = 0.42f;
        public float MagnetRange = 2.2f;
        public float MagnetSpeed = 13f;

        /// <summary>Seconds before an uncollected coin blob evaporates.</summary>
        public float Lifetime = 22f;

        public float ScatterSpeedMin = 3.5f;
        public float ScatterSpeedMax = 8.5f;
        public float Drag = 3.4f;

        /// <summary>Gold per dropped blob; larger drops split into several.</summary>
        public float ClumpSize = 12f;

        /// <summary>Grace period before the victim can re-collect their own dropped gold.</summary>
        public float OwnerPickupDelay = 0.35f;

        /// <summary>Safety cap so the floor cannot fill with thousands of blobs.</summary>
        public int MaxActive = 160;
    }
}
