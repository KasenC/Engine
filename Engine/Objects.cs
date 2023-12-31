﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using System;
using System.Collections.Generic;

namespace Engine
{
    public enum CenterPos { TopLeft, TopMiddle, TopRight, LeftMiddle, Middle, RightMiddle, BottomLeft, BottomMiddle, BottomRight }

    public interface IManager
    {
        internal void AddManaged(ManagedObject managedObject);

        internal void DeleteManaged(ManagedObject managedObject);

        public ControlManager Controls { get; }

        public ContentManager Content { get; }
    }

    public interface IManager<T> : IManager
    {
        internal void AddManaged(T managedObject);

        internal void DeleteManaged(T managedObject);
    }

    public abstract class ManagedObject : IComparable<ManagedObject>
    {
        /// <summary>
        /// Higher value = later update
        /// </summary>
        public readonly float updateOrder;
        internal readonly uint internalId;
        private static uint nextInternalId;

        protected readonly IManager manager;

        public ManagedObject(IManager manager, float updateOrder = 0f)
        {
            internalId = nextInternalId++;
            this.updateOrder = updateOrder;
            this.manager = manager;
            AddToManager();
        }

        public void Delete()
        {
            DeleteFromManager();
            InternalOnDestroy();
        }

        private protected virtual void AddToManager()
        {
            manager.AddManaged(this);
        }

        private protected virtual void DeleteFromManager()
        {
            manager.DeleteManaged(this);
        }

        internal virtual void InternalUpdate(GameTime gameTime)
        {
            Update(gameTime);
        }

        internal virtual void InternalDrawUpdate(GameTime gameTime) 
        {
            DrawUpdate(gameTime);
        }

        internal virtual void InternalOnDestroy()
        {
            OnDestroy();
        }

        protected virtual void Update(GameTime gameTime) { }

        protected virtual void DrawUpdate(GameTime gameTime) { }

        protected virtual void OnDestroy() { }

        protected ControlManager Controls => manager.Controls;

        public int CompareTo(ManagedObject obj)
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

        protected T ContentLoad<T>(string name)
        {
            return manager.Content.Load<T>(name);
        }
    }

    public class Script<T> : ManagedObject where T : ScriptableObject
    {
        public T OwningObject { get; internal set; }

        /// <param name="scriptUpdateOrder">Only determines update order between scripts on the owning object. Overall update order is determined by object update order.</param>
        public Script(T owningObject, float scriptUpdateOrder = 0f): base(owningObject, scriptUpdateOrder)
        {
            OwningObject = owningObject;
        }

        internal override void InternalOnDestroy()
        {
            base.InternalOnDestroy();
            OwningObject = null;
        }
    }

    public class ScriptableObject : ManagedObject, IManager
    {
        public bool active = true;

        private readonly SortedSet<ManagedObject> scripts = new();
        private readonly List<ManagedObject> scriptsToAdd = new(), scriptsToRemove = new();
        private bool lockObjectList = false;

        ControlManager IManager.Controls => manager.Controls;

        ContentManager IManager.Content => manager.Content;

        public ScriptableObject(IManager engine, float updateOrder = 0): base(engine, updateOrder)
        { }

        private void AddManagedObject(ManagedObject managedObject)
        {
            if (!lockObjectList)
                scripts.Add(managedObject);
            else
                scriptsToAdd.Add(managedObject);
        }

        void IManager.AddManaged(ManagedObject managedObject)
        {
            AddManagedObject(managedObject);
        }

        private void DeleteManagedObject(ManagedObject managedObject)
        {
            if (!lockObjectList)
                scripts.Remove(managedObject);
            else
                scriptsToRemove.Add(managedObject);
        }

        void IManager.DeleteManaged(ManagedObject managedObject)
        {
            DeleteManagedObject(managedObject);
        }

        internal override void InternalUpdate(GameTime gameTime)
        {
            if (!active)
                return;
            base.InternalUpdate(gameTime);
            Iterate(x => x.InternalUpdate(gameTime));
        }

        internal override void InternalDrawUpdate(GameTime gameTime)
        {
            if (!active)
                return;
            base.InternalDrawUpdate(gameTime);
            Iterate(x => x.InternalDrawUpdate(gameTime));
        }

        internal override void InternalOnDestroy()
        {
            base.InternalOnDestroy();
            Iterate(x => x.Delete());
        }

        private void Iterate(Action<ManagedObject> action)
        {
            lockObjectList = true;
            foreach (ManagedObject managedObject in scripts)
            {
                action(managedObject);
            }
            lockObjectList = false;

            foreach (var managed in scriptsToAdd)
            {
                AddManagedObject(managed);
            }
            scriptsToAdd.Clear();
            foreach (var managed in scriptsToRemove)
            {
                DeleteManagedObject(managed);
            }
            scriptsToRemove.Clear();
        }
    }

    public class Camera : ScriptableObject
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
