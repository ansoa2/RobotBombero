using RobotBombero.Simulation;

namespace RobotBombero.Game.Tests
{
    internal static class TestData
    {
        public static RulePackFile Pack(string name)
        {
            return new RulePackFile
            {
                formatVersion = PackLimits.CurrentFormatVersion,
                name = name,
                rules = new[]
                {
                    new Rule
                    {
                        name = "r1", battery = "High", temp = "Anyone", light = "Anyone",
                        prox = "Obstacle No", cell = "Anyone", actions = new[] { "Fwd" }
                    },
                    new Rule
                    {
                        name = "r2", battery = "Anyone", temp = "Up", light = "Anyone",
                        prox = "Anyone", cell = "Anyone", actions = new[] { "Put Out", "TurnL" }
                    }
                }
            };
        }

        public static LocalPack Local(string id, string name, long serverId = 0)
        {
            return new LocalPack
            {
                localId = id,
                serverPackId = serverId,
                content = Pack(name),
                stats = new PackStats()
            };
        }
    }
}
