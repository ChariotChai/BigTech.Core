using System;
using System.Collections.Generic;
using System.Text.Json;

namespace BigTech.Model
{

    //public class Entity
    //{
    //    public long Id { get; set; }
    //    public Entity(long id)
    //    {
    //        this.Id = id;
    //    }

    //}

    public struct Entity : IEquatable<Entity>
    {
        public long Value;

        public Entity(long value) => Value = value;

        public bool Equals(Entity other) => Value == other.Value;
        public override bool Equals(object obj) => obj is Entity other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public static bool operator ==(Entity left, Entity right) => left.Equals(right);
        public static bool operator !=(Entity left, Entity right) => !left.Equals(right);
    }

    public abstract class BaseComponent
    {
        public long EntityId;

        public Entity GetEntity(World world)
        {
            return new Entity(EntityId);
        }
    }

    public interface ISystem
    {
        void Run(World world);
    }

    public interface IEventHandler
    {
        void Handle(BaseComponent component);
    }

    public class World
    {
        private long _nextEntityId = 1;
        private Dictionary<Entity, Dictionary<Type, BaseComponent>> _entities = new();
        private Dictionary<Type, List<IEventHandler>> _eventHandlers = new();
        private Dictionary<Entity, List<BaseComponent>> _changedComponents = new();
        private Stack<Action> _transactionStack = new();
        private bool _inTransaction = false;

        // 创建实体
        public Entity CreateEntity()
        {
            var id = new Entity(_nextEntityId++);
            _entities[id] = new Dictionary<Type, BaseComponent>();
            return id;
        }

        // 添加组件
        public void AddComponent<T>(Entity entityId, T component) where T : BaseComponent
        {
            if (!_entities.TryGetValue(entityId, out var components))
            {
                components = new Dictionary<Type, BaseComponent>();
                _entities[entityId] = components;
            }

            if (components.ContainsKey(typeof(T)))
            {
                throw new InvalidOperationException($"Entity {entityId} already has component of type {typeof(T)}");
            }

            components[typeof(T)] = component;
            _changedComponents[entityId] = new List<BaseComponent> { component };

            // 触发事件
            if (_eventHandlers.TryGetValue(typeof(T), out var handlers))
            {
                foreach (var handler in handlers)
                {
                    handler.Handle(component);
                }
            }

            if (_inTransaction)
            {
                _transactionStack.Push(() => RemoveComponent<T>(entityId));
            }
        }

        // 获取组件
        public T GetComponent<T>(Entity entityId) where T : BaseComponent
        {
            if (_entities.TryGetValue(entityId, out var components) &&
                components.TryGetValue(typeof(T), out var component))
            {
                return (T)component;
            }
            throw new KeyNotFoundException($"Entity {entityId} does not have component of type {typeof(T)}");
        }

        public List<T> GetComponents<T>() where T : BaseComponent
        {
            List<T> values = new();
            foreach (var (_, v) in _entities)
            {
                if (v.TryGetValue(typeof(T), out var component))
                {
                    values.Add((T)component);
                }
            }
            return values;
        }

        // 移除组件
        public void RemoveComponent<T>(Entity entityId) where T : BaseComponent
        {
            if (_entities.TryGetValue(entityId, out var components) &&
                components.TryGetValue(typeof(T), out var component))
            {
                components.Remove(typeof(T));
                _changedComponents[entityId] = new List<BaseComponent> { component };

                if (_inTransaction)
                {
                    _transactionStack.Push(() => AddComponent(entityId, component));
                }
            }
        }

        // 注册事件处理器
        public void RegisterEventHandler<T>(IEventHandler handler) where T : BaseComponent
        {
            var type = typeof(T);
            if (!_eventHandlers.TryGetValue(type, out var handlers))
            {
                handlers = new List<IEventHandler>();
                _eventHandlers[type] = handlers;
            }
            handlers.Add(handler);
        }

        // 开始事务
        public void BeginTransaction()
        {
            if (_inTransaction)
            {
                throw new InvalidOperationException("Transaction already in progress");
            }
            _inTransaction = true;
            _transactionStack.Clear();
        }

        // 提交事务
        public void CommitTransaction()
        {
            if (!_inTransaction)
            {
                throw new InvalidOperationException("No transaction in progress");
            }
            _inTransaction = false;
            _transactionStack.Clear();
        }

        // 回滚事务
        public void RollbackTransaction()
        {
            if (!_inTransaction)
            {
                throw new InvalidOperationException("No transaction in progress");
            }

            while (_transactionStack.Count > 0)
            {
                _transactionStack.Pop().Invoke();
            }

            _inTransaction = false;
        }

        // 获取所有变更的组件
        public Dictionary<Entity, List<BaseComponent>> GetChangedComponents()
        {
            var result = new Dictionary<Entity, List<BaseComponent>>(_changedComponents);
            _changedComponents.Clear();
            return result;
        }

        // 序列化
        public string Serialize()
        {
            var data = new Dictionary<string, object>();

            // 序列化实体和组件
            var entitiesData = new Dictionary<string, object>();
            foreach (var (entityId, components) in _entities)
            {
                var componentsData = new Dictionary<string, object>();
                foreach (var (type, component) in components)
                {
                    componentsData[type.FullName] = SerializeComponent(component);
                }
                entitiesData[entityId.Value.ToString()] = componentsData;
            }
            data["entities"] = entitiesData;

            return JsonSerializer.Serialize(data);
        }

        // 反序列化
        public void Deserialize(string json)
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            var entitiesData = (Dictionary<string, object>)data["entities"];

            // 清空当前状态
            _entities.Clear();
            _changedComponents.Clear();

            // 反序列化实体和组件
            foreach (var (entityIdStr, componentsData) in entitiesData)
            {
                var entityId = new Entity(long.Parse(entityIdStr));
                var componentsDict = (Dictionary<string, object>)componentsData;

                var components = new Dictionary<Type, BaseComponent>();
                foreach (var (typeStr, componentData) in componentsDict)
                {
                    var type = Type.GetType(typeStr);
                    if (type == null)
                    {
                        throw new InvalidOperationException($"Type {typeStr} not found");
                    }

                    var component = DeserializeComponent(type, componentData);
                    components[type] = component;
                }

                _entities[entityId] = components;
            }
        }

        private object SerializeComponent(BaseComponent component)
        {
            // 这里可以使用反射或手动序列化具体组件
            return JsonSerializer.Serialize(component);
        }

        private BaseComponent DeserializeComponent(Type type, object data)
        {
            // 这里可以使用反射或手动反序列化具体组件
            var json = data.ToString();
            return (BaseComponent)JsonSerializer.Deserialize(json, type);
        }

    }

}
