using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace Engine
{
    public class GameEngine : Game, IManager<GameObject>
    {
        //Settings
        public static float pixelsPerWorldUnit { get; protected set; } = 1f;
        public bool drawOnPixelGrid = false;
        public bool enableFpsCounter = true;

        //Internals
        private SpriteBatch spriteBatch;
        protected GraphicsDeviceManager graphics;
        protected internal Camera camera;
        protected internal ControlManager controls;

        SpriteFont font;
        SimpleFps fps = new();

        private readonly List<GameObject> gameObjects = new();
        private readonly SortedSet<ManagedObject> managedObjects = new();
        private readonly List<ManagedObject> managedObjectsToRemove = new(), managedObjectsToAdd = new();

        //Execution status flags
        private bool execStatusInit = false, execStatusLoad = false, 
            lockObjLists = false;

        public GameEngine()
        {
            graphics = new GraphicsDeviceManager(this);
            controls = new ControlManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        private void AddManagedObject(ManagedObject managedObject)
        {
            if (!lockObjLists)
                managedObjects.Add(managedObject);
            else
                managedObjectsToAdd.Add(managedObject);
        }

        void IManager.AddManaged(ManagedObject managedObject)
        {
            AddManagedObject(managedObject);
        }

        void IManager<GameObject>.AddManaged(GameObject gameObject)
        {
            gameObjects.Add(gameObject);
            AddManagedObject(gameObject);
        }

        private void DeleteManagedObject(ManagedObject managedObject)
        {
            if(!lockObjLists)
                managedObjects.Remove(managedObject);
            else
                managedObjectsToRemove.Add(managedObject);
        }

        void IManager.DeleteManaged<T>(T managedObject)
        {
            DeleteManagedObject(managedObject);
        }

        void IManager<GameObject>.DeleteManaged(GameObject gameObject)
        {
            gameObjects.Remove(gameObject);
            DeleteManagedObject(gameObject);
        }

        protected override void Initialize()
        {
            execStatusInit = true;
            camera = new Camera(this);
            base.Initialize();
        }

        protected override void LoadContent()
        {
            execStatusLoad = true;
            spriteBatch = new SpriteBatch(GraphicsDevice);
            font = Content.Load<SpriteFont>("Arial10pt");
        }

        protected sealed override void BeginRun()
        {
            if (!execStatusInit)
                throw new InvalidOperationException("BeginRun() called before Engine.Initialize(). If Initialize() or LoadContent() are overridden, you MUST invoke the base implementations, e.g. base.Initialize();");
            if (!execStatusLoad)
                throw new InvalidOperationException("BeginRun() called before LoadContent(). If LoadContent() is overridden, you MUST invoke the base implementation, e.g. base.LoadContent();");
        }

        protected sealed override void Update(GameTime gameTime)
        {
            controls.Update();
            Iterate((o) => o.InternalUpdate(gameTime));
            fps.Update(gameTime);
            base.Update(gameTime);
        }

        protected sealed override void Draw(GameTime gameTime)
        {
            camera.SetWindowBounds(graphics);

            Iterate((o) => o.InternalDrawUpdate(gameTime));

            GraphicsDevice.Clear(Color.CornflowerBlue);
            spriteBatch.Begin();

            static Vector2 QuantizePosition(Vector2 pos) => Vector2.Round(pos * pixelsPerWorldUnit) / pixelsPerWorldUnit;
            if(drawOnPixelGrid)
                camera.position = QuantizePosition(camera.position);

            foreach (var gameObject in gameObjects.OrderBy(x => x.drawOrder))
            {
                if (!gameObject.active || !gameObject.visible || gameObject.Texture == null)
                    continue;

                Vector2 drawPos, drawScale;
                if(gameObject.drawUsesWorldPos)
                {
                    if(!camera.IsOnScreen(gameObject))
                        continue;

                    Vector2 worldPos = gameObject.WorldPos;
                    if (drawOnPixelGrid)
                        worldPos = QuantizePosition(worldPos);
                    drawPos = camera.WorldPosToScreenPos(worldPos).ToVector2();
                    drawScale = gameObject.WorldScale * camera.zoom;
                }
                else
                {
                    drawPos = gameObject.WorldPos;
                    drawScale = gameObject.WorldScale;
                }
                Vector2 visiblePortionOffset = gameObject.TexturePixelVisiblePortion?.Location.ToVector2() ?? Vector2.Zero;
                spriteBatch.Draw(
                    gameObject.Texture,
                    drawPos,
                    gameObject.TexturePixelVisiblePortion,
                    gameObject.ColorMask,
                    gameObject.WorldRotation,
                    gameObject.TexturePixelPivot - visiblePortionOffset, //origin, relative to top left corner of texture, before rotation and scale
                    drawScale,
                    SpriteEffects.None,
                    0f
                );
            }
            fps.DrawFps(spriteBatch, font, new Vector2(5f, 5f), Color.Black);
            spriteBatch.End();

            base.Draw(gameTime);
        }

        private void Iterate(Action<ManagedObject> action)
        {
            lockObjLists = true;
            foreach (ManagedObject managedObject in managedObjects)
            {
                action(managedObject);
            }
            lockObjLists = false;

            foreach (var managed in managedObjectsToAdd)
            {
                AddManagedObject(managed);
            }
            managedObjectsToAdd.Clear();
            foreach (var managed in managedObjectsToRemove)
            {
                DeleteManagedObject(managed);
            }
            managedObjectsToRemove.Clear();
        }
    }

    public enum Side { Top, Right, Bottom, Left }

    public struct Rect
    {
        public float Top = 0f, Left = 0f, Bottom = 0f, Right = 0f;

        public float X
        {
            get => Left;
            set
            {
                float w = Width;
                Left = value;
                Right = value + w;
            }
        }

        public float Y
        {
            get => Top;
            set
            {
                float h = Height;
                Top = value;
                Bottom = value + h;
            }
        }

        public float Width
        {
            get => Right - Left;
            set => Right = Left + value;
        }

        public float Height
        {
            get => Bottom - Top;
            set => Bottom = Top + value;
        }

        public Vector2 Location
        {
            get => new Vector2(X, Y);
            set
            {
                X = value.X;
                Y = value.Y;
            }
        }

        public Vector2 Size
        {
            get => new Vector2(Width, Height);
            set
            {
                Width = value.X;
                Height = value.Y;
            }
        }

        public float Area
        {
            get => Width * Height;
        }

        public Rect(Vector2 location, Vector2 size)
        {
            Location = location;
            Size = size;
        }

        public Rect(float X, float Y, float Width, float Height)
        {
            this.X = X;
            this.Y = Y;
            this.Width = Width;
            this.Height = Height;
        }

        public Rect(Rect rect):this(rect.Location, rect.Size)
        { }

        public Rect Intersect(Rect rect)
        {
            return Intersect(rect, out _);
        }

        public Rect Intersect(Rect rect, out bool intersects)
        {
            Rect intersect = new()
            {
                Top = MathF.Max(Top, rect.Top),
                Bottom = MathF.Min(Bottom, rect.Bottom),
                Left = MathF.Max(Left, rect.Left),
                Right = MathF.Min(Right, rect.Right)
            };
            intersects = intersect.Height > 0f && intersect.Width > 0f;
            return intersect;
        }

        public bool Intersects(Rect rect)
        {
            Intersect(rect, out bool intersects);
            return intersects;
        }

        public float GetSide(Side side)
        {
            if (side == Side.Top)
                return Top;
            if (side == Side.Bottom)
                return Bottom;
            if (side == Side.Left)
                return Left;
            if (side == Side.Right)
                return Right;
            throw new InvalidEnumArgumentException();
        }

        public void Offset(Vector2 amount)
        {
            X += amount.X;
            Y += amount.Y;
        }

        public Rectangle ToRectangle()
        {
            return new Rectangle(Vector2.Round(Location).ToPoint(), Vector2.Round(Size).ToPoint());
        }

        /// <summary>
        /// Returns a new Rect with Location and Size scaled
        /// </summary>
        public Rect ScaleAll(float scale)
        {
            return new Rect(Location * scale, Size * scale);
        }

        /// <summary>
        /// Return a new Rect with Location and Size scaled
        /// </summary>
        public Rect ScaleAll(Vector2 scale)
        {
            return new Rect(Location * scale, Size * scale);
        }
    }

    public abstract class Control
    {
        private bool previousState;
        
        /// <summary>
        /// true during the first update cycle when the Control switches to State true
        /// </summary>
        public bool Pressed { get; private set; }
        
        /// <summary>
        /// true during the first update cycle when the Control switches to State false
        /// </summary>
        public bool Released { get; private set; }

        public abstract bool State { get; }

        internal void Update()
        {
            bool currentState = State;
            if(currentState != previousState)
            {
                Pressed = currentState;
                Released = !currentState;
            }
            else
            {
                Pressed = false;
                Released = false;
            }
            previousState = currentState;
        }
    }

    public class KeyControl : Control
    {
        protected List<Keys> keys;

        public KeyControl(Keys key)
        {
            keys = new() { key };
        }

        public KeyControl(List<Keys> keys)
        {
            this.keys = keys;
        }

        public override bool State { get => Keyboard.GetState().GetPressedKeys().Any(x => keys.Contains(x)); }
    }

    public enum MouseButton { Left, Middle, Right }

    public class MouseButtonControl : Control
    {
        protected MouseButton button;
        internal Func<bool> onScreenCheck;

        public MouseButtonControl(MouseButton button)
        {
            this.button = button;
        }

        private bool MouseIsOnScreen => onScreenCheck?.Invoke() ?? true;

        public override bool State
        {
            get
            {
                if (!MouseIsOnScreen)
                    return false;
                if (button == MouseButton.Left)
                    return Mouse.GetState().LeftButton == ButtonState.Pressed;
                if (button == MouseButton.Middle)
                    return Mouse.GetState().MiddleButton == ButtonState.Pressed;
                if (button == MouseButton.Right)
                    return Mouse.GetState().RightButton == ButtonState.Pressed;
                return false;
            }
        }
    }

    public class ControlManager
    {
        protected Dictionary<int, Control> controls = new();
        protected GameEngine engine;

        public Point MousePos => Mouse.GetState().Position;

        public Vector2 MouseWorldPos => engine.camera.ScreenPosToWorldPos(MousePos);

        public ControlManager(GameEngine engine)
        {
            this.engine = engine;
        }

        internal void Update()
        {
            foreach (Control control in controls.Values)
                control.Update();
        }

        public bool MouseIsOnScreen()
        {
            return engine.camera.IsOnScreen(MousePos);
        }

        public void Add<T>(T id, Control control) where T : Enum
        {
            controls[Convert.ToInt32(id)] = control;
        }

        public void Add<T>(T id, MouseButtonControl mouseControl) where T : Enum
        {
            mouseControl.onScreenCheck = MouseIsOnScreen;
            Add(id, (Control)mouseControl);
        }

        public Control Get<T>(T id) where T : Enum
        {
            controls.TryGetValue(Convert.ToInt32(id), out Control control);
            return control;
        }

        public bool GetState<T>(T id) where T : Enum
        {
            return engine.IsActive && (Get(id)?.State ?? false);
        }

        public bool GetReleased<T>(T id) where T : Enum
        {
            return engine.IsActive && (Get(id)?.Released ?? false);
        }

        public bool GetPressed<T>(T id) where T : Enum
        {
            return engine.IsActive && (Get(id)?.Pressed ?? false);
        }

    }
}