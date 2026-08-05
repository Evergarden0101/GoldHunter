using System.Collections.Generic;
using GoldHunter.Core.Math;

namespace GoldHunter.Core.Navigation
{
    /// <summary>Local steering that A* cannot express, because it is about moving agents.</summary>
    public static class Steering
    {
        /// <summary>
        /// Push away from nearby agents so bots converging on the same popper
        /// spread out instead of grinding into one scrum.
        /// </summary>
        public static Vec2 Separation(Vec2 self, IReadOnlyList<Vec2> others, int selfIndex, float radius)
        {
            Vec2 sum = Vec2.Zero;
            for (int i = 0; i < others.Count; i++)
            {
                if (i == selfIndex) continue;
                Vec2 away = self - others[i];
                float d = away.Magnitude;
                if (d > 1e-4f && d < radius)
                {
                    sum += (away / d) * ((radius - d) / radius);
                }
            }
            return sum;
        }
    }
}
