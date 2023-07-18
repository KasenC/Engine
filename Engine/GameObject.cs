using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Engine
{
    public enum CenterPos { TopLeft, TopMiddle, TopRight, LeftMiddle, Middle, RightMiddle, BottomLeft, BottomMiddle, BottomRight }

    public class GameObject : IManaged
    {

        public bool active = true, visible = true, usesWorldPos = true;
        public Vector2 Position = new(), Scale = Vector2.One, Center = new();
        public float Rotation = 0f, zPos = 0f;
        public Color ColorMask = Color.White;

        public GameObject Parent { get; private set; } = null;

        public Texture2D Texture { get; private set; }
        
        //Object size in world units, with scaling
        public Vector2 Size
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
            get => new Vector2(Texture.Width, Texture.Height);
        }

        //Center of the object relative to the top left corner (in its own reference frame) in world units, after scaling
        public Vector2 ObjectCenter
        {
            get => Center * Size; set => Center = value / Size;
        }

        //Center of the texture from its top left corner in pixels, before scaling
        public Vector2 TextureCenter
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

        private List<IManaged> _scripts = new();
        public List<IManaged> Scripts
        {
            get => new(_scripts);
        }

        private List<GameObject> _children = new();
        public List<GameObject> Children
        {
            get => new(_children);
        }

        public void AddScript(Script script)
        {
            _scripts.Add(script);
            script.gameObject = this;
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

        public void Initialize()
        {
            if (!active)
                return;

            foreach (var script in _scripts)
                script.Initialize();
        }

        public void Update(GameTime gameTime)
        {
            if (!active)
                return;

            foreach (var script in _scripts)
                script.Update(gameTime);
        }

        public void DrawUpdate(GameTime gameTime)
        {
            if (!active)
                return;

            foreach (var script in _scripts)
                script.DrawUpdate(gameTime);
        }
    }

}
