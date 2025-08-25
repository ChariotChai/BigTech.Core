using BigTech.Model.MiniEcs;
using System;
using System.Collections.Generic;

namespace BigTech.Model.Mvp1
{

    public class PlayerComponent : IComponent
    {
        public int Level;
    }

    public class HeroComponent : IComponent
    {
        public int Atk;
        public int Def;
        public int Hp;
        public int MaxHp;
        public int Level;

        public EntityId ownerPlayerId;
    }

    public class SkillComponent : IComponent
    {
        public bool Enabled;
        public int Level;

        public EntityId ownerHeroId;
    }

    public enum BuffType
    {
        AddAttack,
        AddAttackRate,
        AddDefense,
        AddDefenseRate,
        AddHp,
        AddHpRate,
        AddMaxHp,
    }

    public interface IBuff
    {

    }

    public class HeroInternalSystem : IDependencySystem
    {
        public IEnumerable<Type> InputTypes => new[] { typeof(HeroComponent) };

        public IEnumerable<Type> OutputTypes => new[] { typeof(HeroComponent) };

        public void Execute(MiniEcs.World world, IEnumerable<MiniEcs.Entity> entities)
        {
            foreach (var entity in entities)
            {
                var hero = entity.Get<HeroComponent>();
                Console.WriteLine($"HeroInternalSystem: Hero Level {hero.Level}, Atk {hero.Atk}, Def {hero.Def}, Hp {hero.Hp}/{hero.MaxHp}");

                hero.MaxHp = 10 + 5 * hero.Level;
                hero.Atk = 5 + (int)(1.5 * hero.Level);
                hero.Def = 3 + (int)(1.2 * hero.Level);
            }
        }
    }

    public class HeroLevelUpSystem
    {
        public void LevelUp(MiniEcs.World world, EntityId heroId)
        {
            var heroEntity = world.Find(heroId);
            if (heroEntity == null) return;
            var hero = heroEntity.Get<HeroComponent>();
            if (hero == null) return;
            hero.Level += 1;
            Console.WriteLine($"HeroLevelUpSystem: Hero {heroId} leveled up to {hero.Level}");
        }
    }

}