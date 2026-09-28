using System.Collections.Generic;

namespace CloseTheDeal.Tower
{
    public struct FloorSpec
    {
        public FloorKind Kind;
        /// <summary>Index into the profile's Offices for ordinary floors; 0 otherwise.</summary>
        public int TemplateIndex;
    }

    /// <summary>
    /// Turns a seed into the ordered list of floors, bottom to top. Pure and deterministic:
    /// the same profile and seed give the same list on every machine, which is what lets the
    /// host send one number instead of a building.
    /// </summary>
    public static class TowerLayout
    {
        public static void Build(TowerProfile profile, uint seed, List<FloorSpec> into)
        {
            into.Clear();
            var random = new Unity.Mathematics.Random(seed == 0 ? 1u : seed);
            int officeCount = profile.Offices != null ? profile.Offices.Length : 0;

            into.Add(new FloorSpec { Kind = FloorKind.Lobby });

            int sinceCheckpoint = 0;
            int previousOffice = -1;
            for (int i = 0; i < profile.RandomFloors; i++)
            {
                int office = PickOffice(ref random, officeCount, previousOffice);
                previousOffice = office;
                into.Add(new FloorSpec { Kind = FloorKind.Office, TemplateIndex = office });

                sinceCheckpoint++;
                bool lastOffice = i == profile.RandomFloors - 1;
                if (profile.CheckpointEvery > 0 && sinceCheckpoint >= profile.CheckpointEvery && !lastOffice)
                {
                    into.Add(new FloorSpec { Kind = FloorKind.Checkpoint });
                    sinceCheckpoint = 0;
                }
            }

            into.Add(new FloorSpec { Kind = FloorKind.Boardroom });
            into.Add(new FloorSpec { Kind = FloorKind.Roof });
        }

        /// <summary>Random office template, never the same one twice in a row when there is a choice.</summary>
        static int PickOffice(ref Unity.Mathematics.Random random, int count, int previous)
        {
            if (count <= 1)
                return 0;

            int pick = random.NextInt(0, count - 1);
            if (pick >= previous && previous >= 0)
                pick++;
            return pick;
        }
    }
}
