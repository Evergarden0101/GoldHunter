

namespace GoldHunter.Core.Simulation
{
    /// <summary>Per-player tallies, shown on the results board.</summary>
    public sealed class PlayerStats
    {
        public float Mined;
        public float Banked;

        /// <summary>Gold taken off other players with punches.</summary>
        public float Robbed;

        /// <summary>Gold taken out of enemy vaults with Steal.</summary>
        public float RaidedFor;

        /// <summary>Gold lost to others, whether punched out or raided.</summary>
        public float Lost;

        public float Spent;
        public float SpentFromVault;

        public int PunchesLanded;
        public int PunchesTaken;
        public int VaultRaids;

        public void Reset()
        {
            Mined = Banked = Robbed = RaidedFor = Lost = Spent = SpentFromVault = 0f;
            PunchesLanded = PunchesTaken = VaultRaids = 0;
        }
    }
}
