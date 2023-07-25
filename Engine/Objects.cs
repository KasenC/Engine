using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Engine
{
    public enum CenterPos { TopLeft, TopMiddle, TopRight, LeftMiddle, Middle, RightMiddle, BottomLeft, BottomMiddle, BottomRight }

    public interface IManager<T> where T: ManagedObjectBase<T>
    {
        internal void AddManaged(T managedObject);

        internal GameEngine Engine { get; }
    }

    public interface IGameObjectManager: IManager<ManagedObject>
    {
        internal void AddGameObject(GameObject gameObject);
    }

    public abstract class ManagedObjectBase<T> : IComparable<ManagedObjectBase<T>> where T: ManagedObjectBase<T>
    {
        /// <summary>
        /// Higher value = later update
        /// </summary>
        private readonly float updateOrder;
        internal readonly uint internalId;
        private static uint nextInternalId;

        private protected readonly GameEngine engine;

        public ManagedObjectBase(IManager<T> manager, float updateOrder = 0f)
        {
            internalId = nextInternalId++;
            this.updateOrder = updateOrder;
            engine = manager.Engine;
            manager.AddManaged((T)this);
        }

        internal virtual void InternalUpdate(GameTime gameTime)
        {
            Update(gameTime);
        }

        internal virtual void InternalDrawUpdate(GameTime gameTime) 
        {
            DrawUpdate(gameTime);
        }

        internal virtual void InternalDestroy()
        {
            Destroy();
        }

        protected virtual void Update(GameTime gameTime) { }

        protected virtual void DrawUpdate(GameTime gameTime) { }

        protected virtual void Destroy() { }

        protected ControlManager Controls => engine.controls;

        protected IGameObjectManager Manager => engine;

        protected void DestroyGameObject(GameObject gameObject)
        {
            engine.DestroyGameObject(gameObject);
        }
        protected void DestroyManagedObject(ManagedObject managedObject)
        {
            engine.DestroyManagedObject(managedObject);
        }

        public int CompareTo(ManagedObjectBase<T> obj)
        {
            int updatePriorityComparison = updateOrder.CompareTo(obj.updateOrder);
            if (updatePriorityComparison != 0)
                return updatePriorityComparison;

            return internalId.CompareTo(obj.internalId);
        }

        protected static float TimeStep(GameTime gameTime)
        {
            return (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        protected U ContentLoad<U>(string name)
        {
            return engine.Content.Load<U>(name);
        }
    }

    public class ManagedObject: ManagedObjectBase<ManagedObject>
    {
        public ManagedObject(IManager<ManagedObject> engine, float updateOrder = 0f):base(engine, updateOrder)
        { }
    }

    public class Script<T> : ManagedObjectBase<Script<T>> where T : ScriptableObject<T>
    {
        public T OwningObject { get; internal set; }

        /// <param name="scriptUpdateOrder">Only determines update order between scripts on the owning object. Overall update order is determined by object update order.</param>
        public Script(T owningObject, float scriptUpdateOrder = 0f): base(owningObject, scriptUpdateOrder)
        {
        }

        internal override void InternalDestroy()
        {
            base.InternalDestroy();
            OwningObject = null;
        }
    }

    public class ScriptableObject<T> : ManagedObject, IManager<Script<T>> where T: ScriptableObject<T>
    {
        public bool active = true;

        private SortedSet<Script<T>> _scripts = new();
        public List<Script<T>> Scripts
        {
            get => new(_scripts);
        }

        GameEngine IManager<Script<T>>.Engine => engine;

        public ScriptableObject(IManager<ManagedObject> engine, float updateOrder = 0): base(engine, updateOrder)
        { }

        void IManager<Script<T>>.AddManaged(Script<T> script)
        {
            _scripts.Add(script);
            script.OwningObject = (T)this;
        }

        internal override void InternalUpdate(GameTime gameTime)
        {
            if (!active)
                return;
            base.InternalUpdate(gameTime);
            foreach (var script in _scripts)
                script.InternalUpdate(gameTime);
        }

        internal override void InternalDrawUpdate(GameTime gameTime)
        {
            if (!active)
                return;
            base.InternalDrawUpdate(gameTime);
            foreach (var script in _scripts)
                script.InternalDrawUpdate(gameTime);
        }

        internal override void InternalDestroy()
        {
            base.InternalDestroy();
            foreach (var script in _scripts)
                script.InternalDestroy();
            _scripts.Clear();
        }
    }

    public class Camera : ScriptableObject<Camera>
    {
        public Vector2 position;
        public float zoom = 1f;

        //Size of game window (screen space)
        public Point WindowBounds { get; private protected set; }

        //Size of area displayed by camera (world space)
        public Vector2 ViewPortSize { get => WindowBounds.ToVector2() / WorldScaleToScreenScale; }

        public Camera(IManager<ManagedObject> engine, float updateOrder = 0f):base(engine, updateOrder)
        { }

        //Zoom camera so that the viewport is width units wide (world space)
        public void SetViewPortWidth(float width)
        {
            zoom = width * GameEngine.pixelsPerWorldUnit / WindowBounds.X;
        }

        //Zoom camera so that the viewport is height units tall (world space)
        public void SetViewPortHeight(float height)
        {
            zoom = height * GameEngine.pixelsPerWorldUnit / WindowBounds.Y;
        }

        internal void SetWindowBounds(GraphicsDeviceManager _graphics)
        {
            WindowBounds = new(_graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
        }

        public bool IsOnScreen(Point screenPos)
        {
            return screenPos.X >= 0 && screenPos.Y >= 0 && screenPos.X <= WindowBounds.X && screenPos.Y <= WindowBounds.Y;
        }

        public bool IsOnScreen(GameObject gameObject)
        {
            Rect bounds = gameObject.WorldBounds;
            float maxX = MathF.Max(MathF.Abs(bounds.Left), MathF.Abs(bounds.Right)),
                maxY = MathF.Max(MathF.Abs(bounds.Top), MathF.Abs(bounds.Bottom));
            int maxRadius = (int)MathF.Ceiling(WorldScaleToScreenScale * MathF.Sqrt(maxX * maxX + maxY * maxY));
            Point pos = WorldPosToScreenPos(gameObject.WorldPos);

            return pos.X + maxRadius >= 0 && pos.X - maxRadius <= WindowBounds.X && pos.Y + maxRadius >= 0 && pos.Y - maxRadius <= WindowBounds.Y;
        }

        public float WorldScaleToScreenScale
        {
            get => zoom * GameEngine.pixelsPerWorldUnit;
        }

        public Point WorldPosToScreenPos(Vector2 worldPos)
        {
            return Vector2.Round((worldPos - position) * WorldScaleToScreenScale).ToPoint() + WindowBounds / new Point(2);
        }

        public Vector2 ScreenPosToWorldPos(Point screenPos)
        {
            return (screenPos - WindowBounds / new Point(2)).ToVector2() / WorldScaleToScreenScale + position;
        }
    }
}
