using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Engine
{
    public enum CenterPos { TopLeft, TopMiddle, TopRight, LeftMiddle, Middle, RightMiddle, BottomLeft, BottomMiddle, BottomRight }

    public abstract class ManagedObject
    {
        internal GameEngine Engine { protected private get; set; }

        internal virtual void InternalInitialize() 
        {
            Initialize();
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

        public virtual void Initialize() { }

        public virtual void Update(GameTime gameTime) { }

        public virtual void DrawUpdate(GameTime gameTime) { }

        public virtual void Destroy() { }

        protected ControlManager Controls => Engine.controls;

        protected GameObject CreateGameObject()
        {
            if (Engine == null)
                throw new InvalidOperationException("engine reference has not been set. This ManagedObject may be being used improperly.");
            return Engine.CreateGameObject();
        }

        protected void DestroyGameObject(GameObject gameObject)
        {
            if (Engine == null)
                throw new InvalidOperationException("engine reference has not been set. This ManagedObject may be being used improperly.");
            Engine.DestroyGameObject(gameObject);
        }

        protected void AddManagedObject(ManagedObject managedObject)
        {
            if (Engine == null)
                throw new InvalidOperationException("engine reference has not been set. This ManagedObject may be being used improperly.");
            Engine.AddManagedObject(managedObject);
        }

        protected void DestroyManagedObject(ManagedObject managedObject)
        {
            if (Engine == null)
                throw new InvalidOperationException("engine reference has not been set. This ManagedObject may be being used improperly.");
            Engine.DestroyManagedObject(managedObject);
        }
    }

    public class Script<T> : ManagedObject where T : ScriptableObject<T>
    {
        public T OwningObject;

        internal override void InternalDestroy()
        {
            base.InternalDestroy();
            OwningObject = null;
        }
    }

    public class ScriptableObject<T> : ManagedObject where T: ScriptableObject<T>
    {
        public bool active = true;

        private List<ManagedObject> _scripts = new();
        public List<ManagedObject> Scripts
        {
            get => new(_scripts);
        }

        public void AddScript(Script<T> script)
        {
            script.Engine = Engine;
            _scripts.Add(script);
            script.OwningObject = (T)this;
        }

        internal override void InternalInitialize()
        {
            if (!active)
                return;
            base.InternalInitialize();
            foreach (var script in _scripts)
                script.InternalInitialize();
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

    public class GameObject : ScriptableObject<GameObject>
    {
        public bool visible = true, usesWorldPos = true;
        public Vector2 Position = new(), Scale = Vector2.One, Center = new();
        public float Rotation = 0f, zPos = 0f;
        public Color ColorMask = Color.White;

        public GameObject Parent { get; private set; } = null;

        public Texture2D Texture { get; private set; }
        
        /// <summary>
        /// Object size in world units, with scaling
        /// </summary>
        public Vector2 ObjectSize
        {
            get
            {
                if (Texture == null)
                    return Vector2.Zero;
                return Scale * TextureSize / GameEngine.pixelsPerWorldUnit;
            }
            set
            {
                if (Texture == null)
                    throw new NullReferenceException("Attempted to set size with null texture");
                Scale = value / (TextureSize / GameEngine.pixelsPerWorldUnit);
            }
        }

        /// <summary>
        /// Texture size in pixels, without scaling
        /// </summary>
        public Vector2 TextureSize { get => Texture.Bounds.Size.ToVector2(); }

        /// <summary>
        /// Center of the object relative to the top left corner (in its own reference frame) in world units, after scaling
        /// </summary>
        public Vector2 ObjectPivot { get => Center * ObjectSize; set => Center = value / ObjectSize; }

        /// <summary>
        /// Center of the texture from its top left corner in pixels, before scaling
        /// </summary>
        public Vector2 TexturePivot { get => Center * TextureSize; set => Center = value / TextureSize; }

        /// <summary>
        /// Rect representing the bounds of the object in world units, before rotation, after scaling, centered at the pivot
        /// </summary>
        public Rect ObjectBounds { get => new Rect(-ObjectPivot, ObjectSize); }

        /// <summary>
        /// Rect representing the bounds of the texture in pixels, before rotation and scaling, centered at the pivot
        /// </summary>
        public Rect TextureBounds { get => new Rect(-TexturePivot, TextureSize); }

        public CenterPos CenterPos
        { 
            set {
                float x, y;

                if (value == CenterPos.TopLeft || value == CenterPos.LeftMiddle || value == CenterPos.BottomLeft)
                    x = 0f;
                else if (value == CenterPos.TopMiddle || value == CenterPos.Middle || value == CenterPos.BottomMiddle)
                    x = 0.5f;
                else
                    x = 1f;

                if (value == CenterPos.TopLeft || value == CenterPos.TopMiddle || value == CenterPos.TopRight)
                    y = 0f;
                else if (value == CenterPos.LeftMiddle || value == CenterPos.Middle || value == CenterPos.RightMiddle)
                    y = 0.5f;
                else
                    y = 1f;

                Center = new Vector2(x, y);
            }
        }

        private List<GameObject> _children = new();
        public List<GameObject> Children
        {
            get => new(_children);
        }

        //Constructor is internal to enforce GameObject creation through GameEngine
        internal GameObject()
        { }

        internal void AddChild(GameObject child)
        {
            child.Parent?.RemoveChild(child);
            _children.Add(child);
        }

        internal void RemoveChild(GameObject child)
        {
            if (child.Parent == this)
            {
                child.Parent = null;
            }
            if (_children.Contains(child))
            {
                _children.Remove(child);
            }
        }

        public void SetTexture(Texture2D texture, Vector2? center = null, Vector2? scale = null)
        {
            Texture = texture;
            if(scale.HasValue)
                Scale = scale.Value;
            if(center != null)
                Center = center.Value;
        }

        public void SetTexture(Texture2D texture, CenterPos center, Vector2? scale = null)
        {
            SetTexture(texture, null, scale);
            CenterPos = center;
        }

        internal override void InternalDestroy()
        {
            base.InternalDestroy();
            foreach (var child in Children)
                RemoveChild(child);
            Parent?.RemoveChild(this);
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
            Rect bounds = gameObject.ObjectBounds;
            float maxX = MathF.Max(MathF.Abs(bounds.Left), MathF.Abs(bounds.Right)),
                maxY = MathF.Max(MathF.Abs(bounds.Top), MathF.Abs(bounds.Bottom));
            int maxRadius = (int)MathF.Ceiling(WorldScaleToScreenScale * MathF.Sqrt(maxX * maxX + maxY * maxY));
            Point pos = WorldPosToScreenPos(gameObject.Position);

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
