using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Engine
{
    public enum CenterPos { TopLeft, TopMiddle, TopRight, LeftMiddle, Middle, RightMiddle, BottomLeft, BottomMiddle, BottomRight }

    public abstract class ManagedObject
    {
        public virtual void Initialize() { }

        public virtual void Update(GameTime gameTime) { }

        public virtual void DrawUpdate(GameTime gameTime) { }
    }

    public class Script<T> : ManagedObject where T : ScriptableObject<T>
    {
        public T owningObject;
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
            _scripts.Add(script);
            script.owningObject = (T)this;
        }

        public override void Initialize()
        {
            if (!active)
                return;

            foreach (var script in _scripts)
                script.Initialize();
        }

        public override void Update(GameTime gameTime)
        {
            if (!active)
                return;

            foreach (var script in _scripts)
                script.Update(gameTime);
        }

        public override void DrawUpdate(GameTime gameTime)
        {
            if (!active)
                return;

            foreach (var script in _scripts)
                script.DrawUpdate(gameTime);
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
        
        //Object size in world units, with scaling
        public Vector2 ObjectSize
        {
            get
            {
                if (Texture == null)
                    return Vector2.Zero;
                return Scale * Texture.Bounds.Size.ToVector2() / GameEngine.pixelsPerWorldUnit;
            }
            set
            {
                if (Texture == null)
                    throw new NullReferenceException("Attempted to set size with null texture");
                Scale = value / (Texture.Bounds.Size.ToVector2() / GameEngine.pixelsPerWorldUnit);
            }
        }

        //Texture size in pixels, without scaling
        public Vector2 TextureSize
        {
            get => Texture.Bounds.Size.ToVector2();
        }

        //Center of the object relative to the top left corner (in its own reference frame) in world units, after scaling
        public Vector2 ObjectPivot
        {
            get => Center * ObjectSize; set => Center = value / ObjectSize;
        }

        //Center of the texture from its top left corner in pixels, before scaling
        public Vector2 TexturePivot
        {
            get => Center * TextureSize; set => Center = value / TextureSize;
        }

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

        /*
        public void SetTexture(string textureName, Vector2 size, Vector2 center, bool absoluteCenter = false)
        {
            
            SetLoadFunc(textureName, (texture) => SetupTexture(texture, size, center, absoluteCenter));
        }

        public void SetTexture(string textureName, Vector2? size = null, CenterPos center = CenterPos.TopLeft)
        {
            SetLoadFunc(textureName, (texture) => SetupTexture(texture, size, center));
        }
        */

        //Constructor is internal to enforce GameObject creation through GameEngine
        internal GameObject() { }

    }

    public class Camera : ScriptableObject<Camera>
    {
        public Vector2 position;
        public float zoom = 1f;

        //Size of game window (screen space)
        public Vector2 WindowBounds { get; private protected set; }

        //Size of area displayed by camera (world space)
        public Vector2 ViewPortSize { get => WindowBounds / WorldScaleToScreenScale; }

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

        public bool IsOnScreen(GameObject gameObject)
        {
            Vector2 TLOffset = gameObject.ObjectPivot, BROffset = gameObject.ObjectSize - gameObject.ObjectPivot;
            float maxX = MathF.Max(TLOffset.X, BROffset.X), maxY = MathF.Max(TLOffset.Y, BROffset.Y);
            float maxRadius = WorldScaleToScreenScale * MathF.Sqrt(maxX * maxX + maxY * maxY);
            Vector2 pos = WorldPosToScreenPos(gameObject.Position);

            return pos.X + maxRadius >= 0f && pos.X - maxRadius <= WindowBounds.X && pos.Y + maxRadius >= 0 && pos.Y - maxRadius <= WindowBounds.Y;
        }

        public Vector2 WorldPosToScreenPos(Vector2 worldPos)
        {
            return (worldPos - position) * WorldScaleToScreenScale + WindowBounds / 2f;
        }

        public float WorldScaleToScreenScale
        {
            get => zoom * GameEngine.pixelsPerWorldUnit;
        }
    }
}
