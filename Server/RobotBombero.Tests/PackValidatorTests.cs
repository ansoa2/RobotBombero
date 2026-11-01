using System;
using RobotBombero.Simulation;
using Xunit;

namespace RobotBombero.Tests
{
    public class PackValidatorTests
    {
        private static Rule MakeRule(string name = "r", params string[] actions)
        {
            return new Rule
            {
                name = name,
                battery = "High",
                temp = "Up",
                light = "Normal",
                prox = "Obstacle No",
                cell = "Normal",
                actions = actions.Length == 0 ? new[] { "Fwd" } : actions
            };
        }

        private static RulePackFile MakePack(params Rule[] rules)
        {
            return new RulePackFile
            {
                formatVersion = PackLimits.CurrentFormatVersion,
                name = "pack",
                rules = rules.Length == 0 ? new[] { MakeRule() } : rules
            };
        }

        [Fact]
        public void Valid_Pack_Passes()
        {
            var result = PackValidator.Validate(MakePack());
            Assert.True(result.IsValid, result.Error);
        }

        [Fact]
        public void Anyone_Passes()
        {
            var r = MakeRule();
            r.battery = r.temp = r.light = r.prox = r.cell = "Anyone";
            Assert.True(PackValidator.Validate(MakePack(r)).IsValid);
        }

        [Fact]
        public void Null_Or_NullRules_Fails()
        {
            Assert.False(PackValidator.Validate(null).IsValid);
            var pack = MakePack();
            pack.rules = null;
            Assert.False(PackValidator.Validate(pack).IsValid);
        }

        [Fact]
        public void EmptyRules_Fails()
        {
            var pack = MakePack();
            pack.rules = new Rule[0];
            Assert.False(PackValidator.Validate(pack).IsValid);
        }

        [Fact]
        public void NullRuleEntry_Fails()
        {
            var pack = MakePack(MakeRule());
            pack.rules = new Rule[] { null };
            Assert.False(PackValidator.Validate(pack).IsValid);
        }

        [Fact]
        public void NullActions_Fails()
        {
            var r = MakeRule();
            r.actions = null;
            Assert.False(PackValidator.Validate(MakePack(r)).IsValid);
        }

        [Fact]
        public void NullActionEntry_Fails()
        {
            var r = MakeRule();
            r.actions = new string[] { null };
            Assert.False(PackValidator.Validate(MakePack(r)).IsValid);
        }

        [Fact]
        public void NullCondition_Fails()
        {
            foreach (var field in new[] { "battery", "temp", "light", "prox", "cell" })
            {
                var r = MakeRule();
                switch (field)
                {
                    case "battery": r.battery = null; break;
                    case "temp": r.temp = null; break;
                    case "light": r.light = null; break;
                    case "prox": r.prox = null; break;
                    default: r.cell = null; break;
                }
                Assert.False(PackValidator.Validate(MakePack(r)).IsValid, field);
            }
        }

        [Fact]
        public void Unknown_ConditionValue_Fails()
        {
            var r = MakeRule();
            r.battery = "Huge";
            Assert.False(PackValidator.Validate(MakePack(r)).IsValid);
            r = MakeRule();
            r.cell = "Fire";
            Assert.False(PackValidator.Validate(MakePack(r)).IsValid);
        }

        [Fact]
        public void Unknown_Action_Fails()
        {
            Assert.False(PackValidator.Validate(MakePack(MakeRule("r", "Jump"))).IsValid);
        }

        [Fact]
        public void Empty_Actions_Fails()
        {
            var r = MakeRule();
            r.actions = new string[0];
            Assert.False(PackValidator.Validate(MakePack(r)).IsValid);
        }

        [Fact]
        public void SixActions_Fails()
        {
            var r = MakeRule("r", "Fwd", "Fwd", "Fwd", "Fwd", "Fwd", "Fwd");
            Assert.False(PackValidator.Validate(MakePack(r)).IsValid);
        }

        [Fact]
        public void FiveActions_Passes()
        {
            var r = MakeRule("r", "Fwd", "Put Out", "TurnL", "TurnR", "Fwd");
            Assert.True(PackValidator.Validate(MakePack(r)).IsValid);
        }

        [Fact]
        public void TooManyRules_Fails()
        {
            var rules = new Rule[PackLimits.MaxRules + 1];
            for (int i = 0; i < rules.Length; i++) rules[i] = MakeRule();
            Assert.False(PackValidator.Validate(MakePack(rules)).IsValid);
        }

        [Fact]
        public void MaxRules_Passes()
        {
            var rules = new Rule[PackLimits.MaxRules];
            for (int i = 0; i < rules.Length; i++) rules[i] = MakeRule();
            Assert.True(PackValidator.Validate(MakePack(rules)).IsValid);
        }

        [Fact]
        public void LongName_Fails()
        {
            var pack = MakePack();
            pack.name = new string('x', PackLimits.MaxNameLength + 1);
            Assert.False(PackValidator.Validate(pack).IsValid);
        }

        [Fact]
        public void NullOrEmptyName_Fails()
        {
            var pack = MakePack();
            pack.name = null;
            Assert.False(PackValidator.Validate(pack).IsValid);
            pack.name = "";
            Assert.False(PackValidator.Validate(pack).IsValid);
        }

        [Fact]
        public void WrongFormatVersion_Fails()
        {
            var pack = MakePack();
            pack.formatVersion = PackLimits.CurrentFormatVersion + 1;
            Assert.False(PackValidator.Validate(pack).IsValid);
        }

        [Fact]
        public void Hash_IgnoresNames()
        {
            var a = new[] { MakeRule("uno") };
            var b = new[] { MakeRule("dos") };
            Assert.Equal(RulesHash.Compute(a), RulesHash.Compute(b));
        }

        [Fact]
        public void Hash_ChangesWithOrder()
        {
            var r1 = MakeRule("a", "Fwd");
            var r2 = MakeRule("b", "TurnL");
            Assert.NotEqual(RulesHash.Compute(new[] { r1, r2 }), RulesHash.Compute(new[] { r2, r1 }));
        }

        [Fact]
        public void Hash_ChangesWithValue()
        {
            var a = MakeRule();
            var b = MakeRule();
            b.light = "Dark";
            Assert.NotEqual(RulesHash.Compute(new[] { a }), RulesHash.Compute(new[] { b }));
        }

        [Fact]
        public void Hash_NoCollisionAcrossFieldBoundaries()
        {
            var a = MakeRule("r", "Fwd", "TurnL");
            var b = MakeRule("r", "Fwd,TurnL");
            Assert.NotEqual(RulesHash.Compute(new[] { a }), RulesHash.Compute(new[] { b }));
        }
    }
}
