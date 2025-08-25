using BigTech.Model.MiniEcs;
using BigTech.Model.Mvp1;

namespace Test
{
    [TestClass]
    public sealed class TestMvp1
    {
        [TestMethod]
        public void TestMethod1()
        {
            BigTech.Model.MiniEcs.World world = new BigTech.Model.MiniEcs.World();

            world.AddSystem(new BigTech.Model.Mvp1.HeroInternalSystem());
            EntityId? heroId = null;

            for (int i = 0; i < 2; i++)
            {
                var playerEntity = world.Create();
                var playerComp = new BigTech.Model.Mvp1.PlayerComponent();
                playerComp.Level = 1;
                playerEntity.Add<BigTech.Model.Mvp1.PlayerComponent>(playerComp);

                for (int h = 0; h < 3; h++)
                {
                    var heroEntity = world.Create();

                    if (heroId == null)
                    {
                        heroId = heroEntity.Id;
                    }

                    var heroComp = new BigTech.Model.Mvp1.HeroComponent();
                    heroComp.Atk = 10;
                    heroComp.Def = 5;
                    heroComp.Hp = 100;
                    heroComp.MaxHp = 1000;
                    heroComp.Level = 1;
                    heroComp.ownerPlayerId = playerEntity.Id;
                    heroEntity.Add<BigTech.Model.Mvp1.HeroComponent>(heroComp);

                    for (int s = 0; s < 6; s++)
                    {
                        var skillEntity = world.Create();
                        var skillComp = new BigTech.Model.Mvp1.SkillComponent();
                        skillEntity.Add<BigTech.Model.Mvp1.SkillComponent>(skillComp);
                        skillComp.Enabled = true;
                        skillComp.Level = 1;
                        skillComp.ownerHeroId = heroEntity.Id;
                    }
                }

            }

            HeroLevelUpSystem sys = new HeroLevelUpSystem();
            sys.LevelUp(world, heroId!.Value);


        }
    }
}