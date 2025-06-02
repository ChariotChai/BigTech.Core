using BigTech.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

// ================ 基础结构 ================

/// <summary>
/// 组件基类，所有组件必须继承此类
/// </summary>
[DataContract]
public abstract class Component
{
    [DataMember]
    internal Entity Owner { get; set; }

    [JsonIgnore]
    internal bool IsDirty { get; set; }

    /// <summary>
    /// 组件初始化方法，当组件被添加到实体时调用
    /// </summary>
    public virtual void Initialize() { }

    /// <summary>
    /// 组件销毁方法，当组件从实体移除时调用
    /// </summary>
    public virtual void Destroy() { }
}

/// <summary>
/// 实体类，作为组件的容器
/// </summary>
[DataContract]
public class Entity
{
    [DataMember]
    internal Guid Id { get; set; }

    [DataMember]
    private Dictionary<Type, Component> components = new Dictionary<Type, Component>();

    [DataMember]
    private Dictionary<string, Entity> nestedEntities = new Dictionary<string, Entity>();

    [DataMember]
    private List<Entity> nestedEntityList = new List<Entity>();

    [JsonIgnore]
    internal World World { get; set; }

    public Entity()
    {
        Id = Guid.NewGuid();
    }

    /// <summary>
    /// 添加组件到实体
    /// </summary>
    public T AddComponent<T>() where T : Component, new()
    {
        return AddComponent(new T());
    }

    /// <summary>
    /// 添加组件实例到实体
    /// </summary>
    public T AddComponent<T>(T component) where T : Component
    {
        if (components.ContainsKey(typeof(T)))
        {
            throw new InvalidOperationException($"Entity already has a component of type {typeof(T).Name}");
        }

        component.Owner = this;
        components[typeof(T)] = component;
        World?.EventSystem?.TriggerComponentAdded(this, component);
        component.Initialize();
        return component;
    }

    /// <summary>
    /// 获取实体上的组件
    /// </summary>
    public T GetComponent<T>() where T : Component
    {
        if (components.TryGetValue(typeof(T), out var component))
        {
            return (T)component;
        }
        return null;
    }

    /// <summary>
    /// 检查实体是否有特定类型的组件
    /// </summary>
    public bool HasComponent<T>() where T : Component
    {
        return components.ContainsKey(typeof(T));
    }

    /// <summary>
    /// 移除实体上的组件
    /// </summary>
    public bool RemoveComponent<T>() where T : Component
    {
        if (components.TryGetValue(typeof(T), out var component))
        {
            component.Destroy();
            components.Remove(typeof(T));
            World?.EventSystem?.TriggerComponentRemoved(this, component);
            return true;
        }
        return false;
    }

    public bool RemoveComponent(Type componentType)
    {
        if (components.TryGetValue(componentType, out var component))
        {
            component.Destroy();
            components.Remove(componentType);
            World?.EventSystem?.TriggerComponentRemoved(this, component);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 获取实体上的所有组件
    /// </summary>
    public IEnumerable<Component> GetAllComponents()
    {
        return components.Values;
    }

    /// <summary>
    /// 添加嵌套实体（类似OOP中的成员变量）
    /// </summary>
    public void AddNestedEntity(string name, Entity entity)
    {
        if (nestedEntities.ContainsKey(name))
        {
            throw new InvalidOperationException($"Nested entity with name {name} already exists");
        }

        nestedEntities[name] = entity;
        entity.World = World;
    }

    /// <summary>
    /// 添加嵌套实体到列表（类似OOP中的集合成员）
    /// </summary>
    public void AddNestedEntity(Entity entity)
    {
        nestedEntityList.Add(entity);
        entity.World = World;
    }

    /// <summary>
    /// 获取命名的嵌套实体
    /// </summary>
    public Entity GetNestedEntity(string name)
    {
        if (nestedEntities.TryGetValue(name, out var entity))
        {
            return entity;
        }
        return null;
    }

    /// <summary>
    /// 获取嵌套实体列表
    /// </summary>
    public IReadOnlyList<Entity> GetNestedEntities()
    {
        return nestedEntityList.AsReadOnly();
    }

    /// <summary>
    /// 标记实体的组件为脏（已变更）
    /// </summary>
    internal void MarkComponentDirty(Component component)
    {
        component.IsDirty = true;
        World?.EventSystem?.TriggerComponentChanged(this, component);
    }

    /// <summary>
    /// 重置实体的所有组件的脏标记
    /// </summary>
    internal void ResetDirtyFlags()
    {
        foreach (var component in components.Values)
        {
            component.IsDirty = false;
        }
    }
}

/// <summary>
/// 系统基类，所有系统必须继承此类
/// </summary>
public abstract class SystemBase
{
    [JsonIgnore]
    protected World World { get; private set; }

    public void Initialize(World world)
    {
        World = world;
        OnInitialize();
    }

    /// <summary>
    /// 系统初始化方法
    /// </summary>
    protected virtual void OnInitialize() { }

    /// <summary>
    /// 系统更新方法，每帧调用
    /// </summary>
    public virtual void Update(float deltaTime) { }

    /// <summary>
    /// 系统销毁方法
    /// </summary>
    public virtual void Destroy() { }
}

/// <summary>
/// 世界类，管理所有实体和系统
/// </summary>
[DataContract]
public class World
{
    [DataMember]
    private Dictionary<Guid, Entity> entities = new Dictionary<Guid, Entity>();

    [DataMember]
    private List<SystemBase> systems = new List<SystemBase>();

    [DataMember]
    internal EventSystem EventSystem { get; private set; }

    [DataMember]
    private ApiSystem apiSystem;

    [DataMember]
    private TransactionManager transactionManager;

    [DataMember]
    private SerializationManager serializationManager;

    public World()
    {
        EventSystem = new EventSystem(this);
        apiSystem = new ApiSystem(this);
        transactionManager = new TransactionManager(this);
        serializationManager = new SerializationManager(this);
    }

    /// <summary>
    /// 创建新实体
    /// </summary>
    public Entity CreateEntity()
    {
        var entity = new Entity { World = this };
        entities[entity.Id] = entity;
        EventSystem.TriggerEntityCreated(entity);
        return entity;
    }

    /// <summary>
    /// 获取实体
    /// </summary>
    public Entity GetEntity(Guid id)
    {
        if (entities.TryGetValue(id, out var entity))
        {
            return entity;
        }
        return null;
    }

    /// <summary>
    /// 销毁实体
    /// </summary>
    public bool DestroyEntity(Guid id)
    {
        if (entities.TryGetValue(id, out var entity))
        {
            // 先移除所有组件
            var components = entity.GetAllComponents().ToList();
            foreach (Component component in components)
            {
                entity.RemoveComponent(component.GetType());
            }

            entities.Remove(id);
            EventSystem.TriggerEntityDestroyed(entity);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 获取所有实体
    /// </summary>
    public IEnumerable<Entity> GetAllEntities()
    {
        return entities.Values;
    }

    /// <summary>
    /// 注册系统
    /// </summary>
    public T RegisterSystem<T>() where T : SystemBase, new()
    {
        var system = new T();
        systems.Add(system);
        system.Initialize(this);
        return system;
    }


    /// <summary>
    /// 获取系统
    /// </summary>
    public T GetSystem<T>() where T : SystemBase
    {
        return systems.OfType<T>().FirstOrDefault();
    }

    /// <summary>
    /// 注册API方法（简化版本）
    /// </summary>
    public void RegisterMethod<T>(string apiName, Action<T> method) where T : SystemBase
    {
       apiSystem.RegisterMethod(apiName, method);
    }

    /// <summary>
    /// 注册API方法（带返回值）
    /// </summary>
    public void RegisterMethod<T, TResult>(string apiName, Func<T, TResult> method) where T : SystemBase
    {
        apiSystem.RegisterMethod(apiName, method);
    }

    /// <summary>
    /// 更新所有系统
    /// </summary>
    public void Update(float deltaTime)
    {
        foreach (var system in systems)
        {
            system.Update(deltaTime);
        }
    }

    /// <summary>
    /// 执行API调用
    /// </summary>
    public ApiResult ExecuteApi(string apiName, params object[] parameters)
    {
        return apiSystem.Execute(apiName, parameters);
    }

    /// <summary>
    /// 开始事务
    /// </summary>
    public void BeginTransaction()
    {
        transactionManager.BeginTransaction();
    }

    /// <summary>
    /// 提交事务
    /// </summary>
    public void CommitTransaction()
    {
        transactionManager.CommitTransaction();
    }

    /// <summary>
    /// 回滚事务
    /// </summary>
    public void RollbackTransaction()
    {
        transactionManager.RollbackTransaction();
    }

    /// <summary>
    /// 获取自上次调用以来的所有变更
    /// </summary>
    public List<Component> GetChangesSinceLastCheck()
    {
        var changes = new List<Component>();
        foreach (var entity in entities.Values)
        {
            foreach (var component in entity.GetAllComponents())
            {
                if (component.IsDirty)
                {
                    changes.Add(component);
                }
            }
            entity.ResetDirtyFlags();
        }
        return changes;
    }

    /// <summary>
    /// 序列化为JSON
    /// </summary>
    public string Serialize()
    {
        return serializationManager.Serialize();
    }

    /// <summary>
    /// 从JSON反序列化
    /// </summary>
    public static World Deserialize(string json)
    {
        return SerializationManager.Deserialize(json);
    }
}

// ================ 事件系统 ================

/// <summary>
/// 事件系统，处理组件变更等事件
/// </summary>
public class EventSystem
{
    private World world;

    private Dictionary<Type, List<Action<Entity, Component>>> componentAddedHandlers =
        new Dictionary<Type, List<Action<Entity, Component>>>();

    private Dictionary<Type, List<Action<Entity, Component>>> componentRemovedHandlers =
        new Dictionary<Type, List<Action<Entity, Component>>>();

    private Dictionary<Type, List<Action<Entity, Component>>> componentChangedHandlers =
        new Dictionary<Type, List<Action<Entity, Component>>>();

    private List<Action<Entity>> entityCreatedHandlers = new List<Action<Entity>>();
    private List<Action<Entity>> entityDestroyedHandlers = new List<Action<Entity>>();

    public EventSystem(World world)
    {
        this.world = world;
    }

    /// <summary>
    /// 注册组件添加事件处理程序
    /// </summary>
    public void RegisterComponentAddedHandler<T>(Action<Entity, T> handler) where T : Component
    {
        var componentType = typeof(T);
        if (!componentAddedHandlers.TryGetValue(componentType, out var handlers))
        {
            handlers = new List<Action<Entity, Component>>();
            componentAddedHandlers[componentType] = handlers;
        }

        handlers.Add((entity, component) => handler(entity, (T)component));
    }

    /// <summary>
    /// 注册组件移除事件处理程序
    /// </summary>
    public void RegisterComponentRemovedHandler<T>(Action<Entity, T> handler) where T : Component
    {
        var componentType = typeof(T);
        if (!componentRemovedHandlers.TryGetValue(componentType, out var handlers))
        {
            handlers = new List<Action<Entity, Component>>();
            componentRemovedHandlers[componentType] = handlers;
        }

        handlers.Add((entity, component) => handler(entity, (T)component));
    }

    /// <summary>
    /// 注册组件变更事件处理程序
    /// </summary>
    public void RegisterComponentChangedHandler<T>(Action<Entity, T> handler) where T : Component
    {
        var componentType = typeof(T);
        if (!componentChangedHandlers.TryGetValue(componentType, out var handlers))
        {
            handlers = new List<Action<Entity, Component>>();
            componentChangedHandlers[componentType] = handlers;
        }

        handlers.Add((entity, component) => handler(entity, (T)component));
    }

    /// <summary>
    /// 注册实体创建事件处理程序
    /// </summary>
    public void RegisterEntityCreatedHandler(Action<Entity> handler)
    {
        entityCreatedHandlers.Add(handler);
    }

    /// <summary>
    /// 注册实体销毁事件处理程序
    /// </summary>
    public void RegisterEntityDestroyedHandler(Action<Entity> handler)
    {
        entityDestroyedHandlers.Add(handler);
    }

    /// <summary>
    /// 触发组件添加事件
    /// </summary>
    internal void TriggerComponentAdded(Entity entity, Component component)
    {
        var componentType = component.GetType();

        if (componentAddedHandlers.TryGetValue(componentType, out var handlers))
        {
            foreach (var handler in handlers)
            {
                handler(entity, component);
            }
        }
    }

    /// <summary>
    /// 触发组件移除事件
    /// </summary>
    internal void TriggerComponentRemoved(Entity entity, Component component)
    {
        var componentType = component.GetType();

        if (componentRemovedHandlers.TryGetValue(componentType, out var handlers))
        {
            foreach (var handler in handlers)
            {
                handler(entity, component);
            }
        }
    }

    /// <summary>
    /// 触发组件变更事件
    /// </summary>
    internal void TriggerComponentChanged(Entity entity, Component component)
    {
        var componentType = component.GetType();

        if (componentChangedHandlers.TryGetValue(componentType, out var handlers))
        {
            foreach (var handler in handlers)
            {
                handler(entity, component);
            }
        }
    }

    /// <summary>
    /// 触发实体创建事件
    /// </summary>
    internal void TriggerEntityCreated(Entity entity)
    {
        foreach (var handler in entityCreatedHandlers)
        {
            handler(entity);
        }
    }

    /// <summary>
    /// 触发实体销毁事件
    /// </summary>
    internal void TriggerEntityDestroyed(Entity entity)
    {
        foreach (var handler in entityDestroyedHandlers)
        {
            handler(entity);
        }
    }
}

// ================ API系统 ================

/// <summary>
/// API结果类
/// </summary>
public class ApiResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
    public object Result { get; set; }
    public List<Component> Changes { get; set; }

    public ApiResult()
    {
        Changes = new List<Component>();
    }
}

/// <summary>
/// API系统，处理外部API调用
/// </summary>
public class ApiSystem
{
    private World world;
    private Dictionary<string, MethodInfo> apiMethods = new Dictionary<string, MethodInfo>();
    private Dictionary<string, object> apiTargets = new Dictionary<string, object>();

    public ApiSystem(World world)
    {
        this.world = world;
    }

    /// <summary>
    /// 注册API方法
    /// </summary>
    public void RegisterApi(string apiName, MethodInfo method, object target)
    {
        if (apiMethods.ContainsKey(apiName))
        {
            throw new InvalidOperationException($"API method with name {apiName} already registered");
        }

        apiMethods[apiName] = method;
        apiTargets[apiName] = target;
    }

    /// <summary>
    /// 注册API方法（简化版本）
    /// </summary>
    public void RegisterMethod<T>(string apiName, Action<T> method) where T : SystemBase
    {
        var system = world.GetSystem<T>();
        if (system == null)
        {
            throw new InvalidOperationException($"System of type {typeof(T).Name} not registered");
        }

        RegisterApi(apiName, method.Method, system);
    }

    /// <summary>
    /// 注册API方法（带返回值）
    /// </summary>
    public void RegisterMethod<T, TResult>(string apiName, Func<T, TResult> method) where T : SystemBase
    {
        var system = world.GetSystem<T>();
        if (system == null)
        {
            throw new InvalidOperationException($"System of type {typeof(T).Name} not registered");
        }

        RegisterApi(apiName, method.Method, system);
    }

    /// <summary>
    /// 执行API调用
    /// </summary>
    public ApiResult Execute(string apiName, params object[] parameters)
    {
        var result = new ApiResult();

        try
        {
            if (!apiMethods.TryGetValue(apiName, out var method))
            {
                result.Success = false;
                result.ErrorMessage = $"API method {apiName} not found";
                return result;
            }

            var target = apiTargets[apiName];

            // 执行前记录变更
            var preChanges = world.GetChangesSinceLastCheck();

            // 执行API方法
            var returnValue = method.Invoke(target, parameters);

            // 执行后获取变更
            result.Changes = world.GetChangesSinceLastCheck();
            result.Success = true;
            result.Result = returnValue;

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.InnerException?.Message ?? ex.Message;
            return result;
        }
    }
}

// ================ 事务管理 ================

/// <summary>
/// 事务管理器，支持API调用的回滚
/// </summary>
public class TransactionManager
{
    private World world;
    private Stack<Transaction> transactions = new Stack<Transaction>();

    public TransactionManager(World world)
    {
        this.world = world;
    }

    /// <summary>
    /// 开始事务
    /// </summary>
    public void BeginTransaction()
    {
        var transaction = new Transaction(world);
        transactions.Push(transaction);
        transaction.CaptureState();
    }

    /// <summary>
    /// 提交事务
    /// </summary>
    public void CommitTransaction()
    {
        if (transactions.Count == 0)
        {
            throw new InvalidOperationException("No active transaction");
        }

        transactions.Pop();
    }

    /// <summary>
    /// 回滚事务
    /// </summary>
    public void RollbackTransaction()
    {
        if (transactions.Count == 0)
        {
            throw new InvalidOperationException("No active transaction");
        }

        var transaction = transactions.Pop();
        transaction.Rollback();
    }
}

/// <summary>
/// 事务类，保存系统状态以便回滚
/// </summary>
public class Transaction
{
    private World world;
    private Dictionary<Guid, EntityState> entityStates = new Dictionary<Guid, EntityState>();
    private List<Guid> addedEntities = new List<Guid>();
    private List<EntityState> removedEntities = new List<EntityState>();

    public Transaction(World world)
    {
        this.world = world;
    }

    /// <summary>
    /// 捕获当前状态
    /// </summary>
    public void CaptureState()
    {
        foreach (var entity in world.GetAllEntities())
        {
            var entityState = new EntityState(entity);
            entityStates[entity.Id] = entityState;
        }
    }

    /// <summary>
    /// 回滚到保存的状态
    /// </summary>
    public void Rollback()
    {
        // 移除在事务期间添加的实体
        foreach (var entityId in addedEntities)
        {
            world.DestroyEntity(entityId);
        }

        // 恢复被移除的实体
        foreach (var entityState in removedEntities)
        {
            var entity = world.CreateEntity();
            entity.Id = entityState.EntityId;
            entityState.Restore(entity);
        }

        // 恢复实体状态
        foreach (var entity in world.GetAllEntities().ToList())
        {
            if (entityStates.TryGetValue(entity.Id, out var entityState))
            {
                entityState.Restore(entity);
            }
            else
            {
                // 实体存在但不在状态快照中，说明是在事务开始后添加的
                world.DestroyEntity(entity.Id);
            }
        }
    }

    /// <summary>
    /// 实体状态类，用于保存和恢复实体状态
    /// </summary>
    private class EntityState
    {
        public Guid EntityId { get; private set; }
        private Dictionary<Type, ComponentState> componentStates = new Dictionary<Type, ComponentState>();

        public EntityState(Entity entity)
        {
            EntityId = entity.Id;

            foreach (var component in entity.GetAllComponents())
            {
                var componentState = new ComponentState(component);
                componentStates[component.GetType()] = componentState;
            }
        }

        /// <summary>
        /// 恢复实体状态
        /// </summary>
        public void Restore(Entity entity)
        {
            // 移除当前所有组件
            var currentComponents = entity.GetAllComponents().ToList();
            foreach (var component in currentComponents)
            {
                entity.RemoveComponent(component.GetType());
            }

            // 恢复保存的组件
            foreach (var componentState in componentStates.Values)
            {
                var component = componentState.Restore(entity);
                entity.AddComponent(component);
            }
        }
    }

    /// <summary>
    /// 组件状态类，用于保存和恢复组件状态
    /// </summary>
    private class ComponentState
    {
        private Type componentType;
        private Dictionary<string, object> fieldValues = new Dictionary<string, object>();

        public ComponentState(Component component)
        {
            componentType = component.GetType();

            // 保存所有非只读字段的值
            var fields = componentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => !f.IsInitOnly);

            foreach (var field in fields)
            {
                fieldValues[field.Name] = field.GetValue(component);
            }
        }

        /// <summary>
        /// 恢复组件状态
        /// </summary>
        public Component Restore(Entity entity)
        {
            var component = (Component)Activator.CreateInstance(componentType);
            component.Owner = entity;

            // 恢复保存的字段值
            var fields = componentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => !f.IsInitOnly);

            foreach (var field in fields)
            {
                if (fieldValues.TryGetValue(field.Name, out var value))
                {
                    field.SetValue(component, value);
                }
            }

            return component;
        }
    }
}

// ================ 序列化管理 ================

/// <summary>
/// 序列化管理器，处理世界的序列化和反序列化
/// </summary>
public class SerializationManager
{
    private World world;

    public SerializationManager(World world)
    {
        this.world = world;
    }

    /// <summary>
    /// 序列化为JSON
    /// </summary>
    public string Serialize()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.Preserve,
            IncludeFields = true
        };

        return JsonSerializer.Serialize(world, options);
    }

    /// <summary>
    /// 从JSON反序列化
    /// </summary>
    public static World Deserialize(string json)
    {
        var options = new JsonSerializerOptions
        {
            ReferenceHandler = ReferenceHandler.Preserve,
            IncludeFields = true
        };

        return JsonSerializer.Deserialize<World>(json, options);
    }
}

// ================ 使用示例 ================

/// <summary>
/// 位置组件示例
/// </summary>
public class PositionComponent : Component
{
    [DataMember]
    public float X { get; set; }

    [DataMember]
    public float Y { get; set; }

    public void SetPosition(float x, float y)
    {
        X = x;
        Y = y;
        Owner.MarkComponentDirty(this);
    }
}

/// <summary>
/// 速度组件示例
/// </summary>
public class VelocityComponent : Component
{
    [DataMember]
    public float DX { get; set; }

    [DataMember]
    public float DY { get; set; }
}

/// <summary>
/// 移动系统示例
/// </summary>
public class MovementSystem : SystemBase
{
    protected override void OnInitialize()
    {
        // 注册API方法
        //World.RegisterMethod<MovementSystem>("MoveEntity", MoveEntity);
    }

    public override void Update(float deltaTime)
    {
        foreach (var entity in World.GetAllEntities())
        {
            var position = entity.GetComponent<PositionComponent>();
            var velocity = entity.GetComponent<VelocityComponent>();

            if (position != null && velocity != null)
            {
                position.SetPosition(
                    position.X + velocity.DX * deltaTime,
                    position.Y + velocity.DY * deltaTime
                );
            }
        }
    }

    /// <summary>
    /// API方法：移动实体
    /// </summary>
    public void MoveEntity(Guid entityId, float dx, float dy)
    {
        var entity = World.GetEntity(entityId);
        if (entity != null)
        {
            var position = entity.GetComponent<PositionComponent>();
            if (position != null)
            {
                position.SetPosition(position.X + dx, position.Y + dy);
            }
        }
    }
}

/// <summary>
/// 测试程序
/// </summary>
public class TestProgram
{
    public static void RunTest()
    {
        // 创建世界
        var world = new World();

        // 注册系统
        world.RegisterSystem<MovementSystem>();

        // 创建实体
        var entity = world.CreateEntity();
        entity.AddComponent<PositionComponent>();
        entity.AddComponent<VelocityComponent>();

        // 设置初始位置
        var position = entity.GetComponent<PositionComponent>();
        position.SetPosition(10, 20);

        // 注册事件处理器
        world.EventSystem.RegisterComponentChangedHandler<PositionComponent>(
            (e, p) => Console.WriteLine($"Position changed: Entity={e.Id}, X={p.X}, Y={p.Y}")
        );

        // 开始事务
        world.BeginTransaction();

        // 执行API调用
        var result = world.ExecuteApi("MoveEntity", entity.Id, 5, 5);

        if (result.Success)
        {
            Console.WriteLine("API call successful");
            Console.WriteLine($"Changes: {result.Changes.Count}");

            // 提交事务
            world.CommitTransaction();
        }
        else
        {
            Console.WriteLine($"API call failed: {result.ErrorMessage}");
            // 回滚事务
            world.RollbackTransaction();
        }

        // 序列化世界
        var json = world.Serialize();
        Console.WriteLine($"Serialized world: {json.Length} characters");

        // 反序列化世界
        var newWorld = World.Deserialize(json);
        Console.WriteLine($"Deserialized world with {newWorld.GetAllEntities().Count()} entities");
    }
}