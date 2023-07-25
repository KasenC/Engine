using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Engine
{


    public class GameObject : ScriptableObject<GameObject>
    {
        public bool visible = true, drawUsesWorldPos = true;
        public Vector2 Center = new();
        public float drawOrder = 0f;
        public Color ColorMask = Color.White;

        public GameObject Parent { get; private set; } = null;

        public Texture2D Texture { get; private set; }

        private static Vector2 Rotate(Vector2 vector, float angle)
        {
            (float sin, float cos) = MathF.SinCos(angle);
            return new(
                vector.X * cos + vector.Y * sin,
                vector.X * sin + vector.Y * cos);
        }

        /// <summary>
        /// Position of the object, centered at its pivot, in world units/world space
        /// </summary>
        public Vector2 WorldPos
        {
            get
            {
                if (Parent == null)
                    return Position;
                return Rotate(Position, Parent.WorldRotation) * Parent.WorldScale + Parent.WorldPos;
            }
            set
            {
                if (Parent == null)
                {
                    Position = value;
                    return;
                }
                Position = Rotate((value - Parent.WorldPos) / Parent.WorldScale, -Parent.WorldRotation);
            }
        }

        /// <summary>
        /// Position of the object, centered at its pivot, in local units/parent space
        /// </summary>
        public Vector2 Position { get; set; } = new();

        /// <summary>
        /// Object's rotation around its pivot in radians/world space
        /// </summary>
        public float WorldRotation
        {
            get
            {
                if (Parent == null)
                    return Rotation;
                return Rotation + Parent.WorldRotation;
            }
            set
            {
                if (Parent == null)
                {
                    Rotation = value;
                    return;
                }
                Rotation = value - Parent.WorldRotation;
            }
        }

        /// <summary>
        /// Object's rotation around its pivot in radians/parent space
        /// </summary>
        public float Rotation { get; set; }

        /// <summary>
        /// Object's scale factor in world space
        /// </summary>
        public Vector2 WorldScale
        {
            get
            {
                if (Parent == null)
                    return Scale;
                return Scale * Parent.WorldScale;
            }
            set
            {
                if (Parent == null)
                {
                    Scale = value;
                    return;
                }
                Scale = value / Parent.WorldScale;
            }
        }

        /// <summary>
        /// Object's scale factor in parent space
        /// </summary>
        public Vector2 Scale
        {
            get => _scale;
            set
            {
                if (value.X < 0f || value.Y < 0f)
                    throw new ArgumentOutOfRangeException("Scale cannot be negative");
                _scale = value;
            }
        }
        private Vector2 _scale = Vector2.One;

        /// <summary>
        /// Object size in world units/world space
        /// </summary>
        public Vector2 WorldSize
        {
            get
            {
                if (Texture == null)
                    return Vector2.Zero;
                return WorldScale * TextureSize;
            }
            set
            {
                if (Texture == null)
                    throw new NullReferenceException("Attempted to set size with null texture");
                WorldScale = value / TextureSize;
            }
        }

        /// <summary>
        /// Object size in local units/parent space
        /// </summary>
        public Vector2 Size
        {
            get
            {
                if (Texture == null)
                    return Vector2.Zero;
                return Scale * TextureSize;
            }
            set
            {
                if (Texture == null)
                    throw new NullReferenceException("Attempted to set size with null texture");
                Scale = value / TextureSize;
            }
        }

        /// <summary>
        /// Texture size in pixels, without scaling
        /// </summary>
        public Point TexturePixelSize { get => Texture.Bounds.Size; }

        /// <summary>
        /// Texture size in world units, without any object scaling
        /// </summary>
        public Vector2 TextureSize { get => TexturePixelSize.ToVector2() / GameEngine.pixelsPerWorldUnit; }


        /// <summary>
        /// Center of the object relative to the top left corner of its texture, in world units/world space
        /// </summary>
        public Vector2 WorldPivot { get => Center * WorldSize; set => Center = value / WorldSize; }

        /// <summary>
        /// Center of the object relative to the top left corner of its texture, in local units/parent space
        /// </summary>
        public Vector2 Pivot { get => Center * Size; set => Center = value / Size; }

        /// <summary>
        /// Center of the texture from its top left corner in pixels, without scaling
        /// </summary>
        public Vector2 TexturePixelPivot { get => Center * TexturePixelSize.ToVector2(); set => Center = value / TexturePixelSize.ToVector2(); }

        /// <summary>
        /// Center of the texture from its top left corner in world units, without any object scaling
        /// </summary>
        public Vector2 TexturePivot { get => Center * TextureSize; set => Center = value / TextureSize; }


        /// <summary>
        /// Rect representing the bounds of the object in world units, before rotation, centered at the pivot
        /// </summary>
        public Rect WorldBounds { get => new(-WorldPivot, WorldSize); }

        /// <summary>
        /// Rect representing the bounds of the object in local units, before rotation, centered at the pivot
        /// </summary>
        public Rect Bounds { get => new(-Pivot, Size); }

        /// <summary>
        /// Rect representing the bounds of the texture in pixels, before rotation and scaling, centered at the pivot
        /// </summary>
        public Rect TexturePixelBounds { get => new(-TexturePixelPivot, TexturePixelSize.ToVector2()); }

        /// <summary>
        /// Rect representing the bounds of the texture in world units, before rotation and scaling, centered at the pivot
        /// </summary>
        public Rect TextureBounds { get => new(-TexturePivot, TextureSize); }

        /// <summary>
        /// Rectangle representing the portion of the object which will be drawn, in world units, before rotation and scaling, centered at its top left corner.
        /// Object size and pivot will be unaffected, as if the full texture was drawn.
        /// </summary>
        public Rect? WorldVisiblePortion
        {
            get => TextureVisiblePortion?.ScaleAll(WorldScale);
            set => TextureVisiblePortion = value?.ScaleAll(Vector2.One / WorldScale);
        }

        /// <summary>
        /// Rectangle representing the portion of the object which will be drawn, in local units, before rotation and scaling, centered at its top left corner.
        /// Object size and pivot will be unaffected, as if the full texture was drawn.
        /// </summary>
        public Rect? VisiblePortion
        {
            get => TextureVisiblePortion?.ScaleAll(Scale);
            set => TextureVisiblePortion = value?.ScaleAll(Vector2.One / Scale);
        }

        /// <summary>
        /// Rectangle representing the portion of the texture which will be drawn, in pixels, before rotation and scaling, centered at its top left corner.
        /// Object size and pivot will be unaffected, as if the full texture was drawn.
        /// </summary>
        public Rectangle? TexturePixelVisiblePortion
        {
            get => TextureVisiblePortion?.ScaleAll(GameEngine.pixelsPerWorldUnit).ToRectangle();
            set => TextureVisiblePortion = value?.ToRect().ScaleAll(1f / GameEngine.pixelsPerWorldUnit);
        }

        /// <summary>
        /// Rect representing the portion of the texture which will be drawn, in world units, before rotation and scaling, centered at its top left corner.
        /// Object size and pivot will be unaffected, as if the full texture was drawn.
        /// </summary>
        public Rect? TextureVisiblePortion { get; set; } = null;

        public CenterPos CenterPos
        {
            set
            {
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

        private readonly List<GameObject> _children = new();
        public List<GameObject> Children
        {
            get => new(_children);
        }

        public GameObject(IGameObjectManager engine, GameObject parent = null, float updateOrder = 0f):base(engine, updateOrder)
        {
            engine.AddGameObject(this);
            parent?.AddChild(this);
        }

        internal void AddChild(GameObject child)
        {
            child.Parent?.RemoveChild(child);
            child.Parent = this;
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

        public void SetTexture(Texture2D texture, Vector2? center = null)
        {
            Texture = texture;
            if (center != null)
                Center = center.Value;
        }

        public void SetTexture(Texture2D texture, CenterPos center)
        {
            SetTexture(texture, null);
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
}
