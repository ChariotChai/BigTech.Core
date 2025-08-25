using System;
using System.Collections.Generic;
using System.Linq;

namespace BigTech.Model.MiniEcs
{


    // Marker for components
    public interface IComponent { }

    // Simple strongly-typed entity id
    public readonly struct EntityId : IEquatable<EntityId>
    {
        public int Value { get; }
        public EntityId(int value) => Value = value;
        public bool Equals(EntityId other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is EntityId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => $"Entity({Value})";
        public static implicit operator int(EntityId id) => id.Value;
    }

    public sealed class Entity
    {
        private readonly Dictionary<Type, List<IComponent>> _components = new();
        internal World World { get; }
        public EntityId Id { get; }

        internal Entity(World world, EntityId id)
        {
            World = world; Id = id;
        }

        /// <summary>
        /// Add a component instance. Multiple components of the same type are allowed.
        /// </summary>
        public Entity Add<T>(T component) where T : IComponent
        {
            var t = typeof(T);
            if (!_components.TryGetValue(t, out var list))
            {
                list = new List<IComponent>();
                _components[t] = list;
            }
            list.Add(component!);
            World.OnComponentAdded(Id, t);
            return this;
        }

        /// <summary>
        /// Add many components of the same type.
        /// </summary>
        public Entity AddMany<T>(IEnumerable<T> components) where T : IComponent
        {
            foreach (var c in components) Add(c);
            return this;
        }

        public bool Has<T>() where T : IComponent
            => _components.TryGetValue(typeof(T), out var list) && list.Count > 0;

        public bool Has<T>(Func<T, bool> predicate) where T : IComponent
            => _components.TryGetValue(typeof(T), out var list) && list.Cast<T>().Any(predicate);

        /// <summary>
        /// Return the first component of type T (throws if missing).
        /// </summary>
        public T Get<T>() where T : IComponent
        {
            if (!_components.TryGetValue(typeof(T), out var list) || list.Count == 0)
                throw new InvalidOperationException($"Entity {Id} has no component of type {typeof(T).Name}");
            return (T)list[0];
        }

        /// <summary>
        /// Return all components of type T (possibly empty).
        /// </summary>
        public IReadOnlyList<T> GetAll<T>() where T : IComponent
        {
            if (!_components.TryGetValue(typeof(T), out var list)) return Array.Empty<T>();
            // Avoid copying when possible
            return list.Count == 0 ? Array.Empty<T>() : list.Cast<T>().ToList();
        }

        public bool TryGet<T>(out T value) where T : IComponent
        {
            if (_components.TryGetValue(typeof(T), out var list) && list.Count > 0)
            { value = (T)list[0]; return true; }
            value = default!; return false;
        }

        public int RemoveAll<T>() where T : IComponent
        {
            var t = typeof(T);
            if (_components.Remove(t, out var list))
            {
                World.OnComponentRemoved(Id, t);
                return list.Count;
            }
            return 0;
        }

        public int RemoveWhere<T>(Func<T, bool> predicate) where T : IComponent
        {
            var t = typeof(T);
            if (!_components.TryGetValue(t, out var list)) return 0;
            int before = list.Count;
            list.RemoveAll(o => predicate((T)o));
            if (list.Count == 0)
            {
                _components.Remove(t);
                World.OnComponentRemoved(Id, t);
            }
            return before - list.Count;
        }

        internal bool ContainsComponentType(Type t) => _components.ContainsKey(t);
    }

    public interface IRoundSystem
    {
        void Run(World world);
    }

    public interface IDependencySystem
    {
        // 系统依赖的输入类型
        IEnumerable<Type> InputTypes { get; }

        // 系统产生的输出类型
        IEnumerable<Type> OutputTypes { get; }

        // 执行逻辑
        void Execute(World world, IEnumerable<Entity> entities);
    }

    /// <summary>
    /// World keeps entities and type indices for fast lookups.
    /// </summary>
    public sealed class World
    {
        private int _nextId = 1;
        private readonly Dictionary<int, Entity> _entities = new();
        private readonly Dictionary<Type, HashSet<int>> _typeIndex = new();
        private readonly List<IRoundSystem> _roundSystems = new();

        private readonly List<IDependencySystem> _systems = new();
        private readonly Dictionary<Type, List<IDependencySystem>> _consumers = new();
        private readonly Queue<Type> _changed = new();

        private int MaxPropagationDepth { get; set; } = 100;

        public Entity Create()
        {
            var id = new EntityId(_nextId++);
            var e = new Entity(this, id);
            _entities[id.Value] = e;
            return e;
        }

        public Entity? Find(EntityId id)
        {
            return _entities.TryGetValue(id.Value, out var e) ? e : null;
        }

        public bool Destroy(Entity e) => Destroy(e.Id);
        public bool Destroy(EntityId id)
        {
            if (_entities.Remove(id.Value, out var e))
            {
                // Update type index: remove entity id from all sets
                foreach (var kv in _typeIndex)
                    kv.Value.Remove(id.Value);
                return true;
            }
            return false;
        }

        internal void OnComponentAdded(EntityId id, Type t)
        {
            if (!_typeIndex.TryGetValue(t, out var set))
            {
                set = new HashSet<int>();
                _typeIndex[t] = set;
            }
            set.Add(id.Value);
        }

        internal void OnComponentRemoved(EntityId id, Type t)
        {
            if (_typeIndex.TryGetValue(t, out var set))
            {
                set.Remove(id.Value);
                if (set.Count == 0) _typeIndex.Remove(t);
            }
        }

        /// <summary>
        /// Fast lookup by component type using the world index.
        /// </summary>
        public IEnumerable<Entity> FindByType<T>() where T : IComponent
        {
            if (_typeIndex.TryGetValue(typeof(T), out var set))
            {
                foreach (var id in set)
                    if (_entities.TryGetValue(id, out var e)) yield return e;
            }
        }

        /// <summary>
        /// Return all living entities (LINQ-friendly enumerable).
        /// </summary>
        public IEnumerable<Entity> Entities() => _entities.Values;

        /// <summary>
        /// LINQ-y query helpers that ensure entities include specific component types.
        /// Uses the type index for efficiency.
        /// </summary>
        public IEnumerable<Entity> With<T>() where T : IComponent
            => FindByType<T>();

        public IEnumerable<Entity> With<T1, T2>() where T1 : IComponent where T2 : IComponent
            => IntersectIds(FindByType<T1>(), FindByType<T2>());

        public IEnumerable<Entity> With<T1, T2, T3>() where T1 : IComponent where T2 : IComponent where T3 : IComponent
            => IntersectIds(IntersectIds(FindByType<T1>(), FindByType<T2>()), FindByType<T3>());

        private static IEnumerable<Entity> IntersectIds(IEnumerable<Entity> a, IEnumerable<Entity> b)
        {
            // Intersect by Id without allocating large sets when small; adaptively choose approach
            var set = new HashSet<int>(a.Select(e => e.Id.Value));
            foreach (var e in b)
                if (set.Contains(e.Id.Value)) yield return e;
        }

        /// <summary>
        /// Project (Entity, T) pairs to iterate components directly. For same-type multiples, each instance yields one pair.
        /// </summary>
        public IEnumerable<(Entity entity, T component)> Join<T>() where T : IComponent
        {
            foreach (var e in FindByType<T>())
            {
                foreach (var c in e.GetAll<T>())
                    yield return (e, c);
            }
        }

        /// <summary>
        /// Project (Entity, T1, T2) triples across entities that have both T1 and T2.
        /// Each combination of T1xT2 instances is produced (cartesian on the same entity) to handle multi-same-type cases.
        /// </summary>
        public IEnumerable<(Entity entity, T1 a, T2 b)> Join<T1, T2>()
            where T1 : IComponent where T2 : IComponent
        {
            foreach (var e in With<T1, T2>())
            {
                var aList = e.GetAll<T1>();
                var bList = e.GetAll<T2>();
                foreach (var a in aList)
                    foreach (var b in bList)
                        yield return (e, a, b);
            }
        }

        public void ExecuteRoundSystems()
        {
            foreach (var system in _roundSystems)
            {
                system.Run(this);
            }
        }

        public void AddSystem(IDependencySystem sys)
        {
            _systems.Add(sys);
            foreach (var t in sys.InputTypes)
            {
                if (!_consumers.TryGetValue(t, out var list))
                    _consumers[t] = list = new List<IDependencySystem>();
                list.Add(sys);
            }
        }

        internal void NotifyComponentChanged(Type type)
        {
            _changed.Enqueue(type);
            RunPropagation();
        }

        private void RunPropagation()
        {
            int depth = 0;
            while (_changed.Count > 0 && depth < MaxPropagationDepth)
            {
                var current = _changed.ToList();
                _changed.Clear();

                foreach (var t in current)
                {
                    if (!_consumers.TryGetValue(t, out var systems)) continue;

                    foreach (var sys in systems)
                    {
                        var entities = Entities()
                            .Where(e => sys.InputTypes.All(i => e.ContainsComponentType(i)));

                        sys.Execute(this, entities);

                        foreach (var outT in sys.OutputTypes)
                            _changed.Enqueue(outT);
                    }
                }

                depth++;
            }

            if (depth >= MaxPropagationDepth)
                throw new InvalidOperationException("Propagation exceeded max depth");
        }


    }

}
