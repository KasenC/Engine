using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Engine
{
    public class GameEngine : Game
    {
        public static float pixelsPerWorldUnit { get; protected set; } = 1f;
        public bool drawOnPixelGrid = false;

        protected GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;
        
        protected Camera camera = new();
        SpriteFont font;

        private List<GameObject> gameObjects = new(), objectsToRemove = new();
        private List<Tuple<GameObject, GameObject>> objectsToAdd = new();
        private List<ManagedObject> managedObjects = new(), managedObjectsToRemove = new(), managedObjectsToAdd = new();

        //Execution status flags
        private bool execStatusInit = false, execStatusLoad = false, 
            LockObjLists = false;

        public bool enableFpsCounter = true;
        SimpleFps fps = new();

        public GameEngine()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        public GameObject CreateGameObject(GameObject parent = null)
        {
            GameObject gameObject = new();
            AddGameObject(gameObject, parent);
            return gameObject;
        }

        public void AddGameObject(GameObject gameObject, GameObject parent)
        {
            if (!LockObjLists)
            {
                parent?.AddChild(gameObject);
                gameObjects.Add(gameObject);
                if(execStatusInit)
                    gameObject.Initialize();
            }
            else
            {
                objectsToAdd.Add(new(gameObject, parent));
            }
        }

        public void RemoveGameObject(GameObject gameObject)
        {
            if(!LockObjLists)
            {
                gameObjects.Remove(gameObject);
                gameObject.Parent?.RemoveChild(gameObject);
            }
            else
            {
                objectsToRemove.Add(gameObject);
            }
        }

        /// <summary>
        /// Note: non-GameObject managed objects will be updated after GameObjects.
        /// </summary>
        public void AddManagedObject(ManagedObject managedObject)
        {
            if(!LockObjLists)
            {
                managedObjects.Add(managedObject);
                if (execStatusInit)
                    managedObject.Initialize();
            }
            else
            {
                managedObjectsToAdd.Add(managedObject);
            }
        }

        public void RemoveManagedObject(ManagedObject managedObject)
        {
            if(!LockObjLists)
            {
                managedObjects.Remove(managedObject);
            }
            else
            {
                managedObjectsToRemove.Add(managedObject);
            }
        }

        protected override void Initialize()
        {
            execStatusInit = true;
            Iterate((o) => o.Initialize());

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
            Iterate((o) => o.Update(gameTime));
            fps.Update(gameTime);
            base.Update(gameTime);
        }

        protected sealed override void Draw(GameTime gameTime)
        {
            camera.SetWindowBounds(graphics);

            Iterate((o) => o.DrawUpdate(gameTime));

            GraphicsDevice.Clear(Color.CornflowerBlue);
            spriteBatch.Begin();
            foreach(var gameObject in gameObjects)
            {
                if (!gameObject.active || !gameObject.visible || gameObject.Texture == null)
                    continue;

                Vector2 drawPos, drawScale, worldPos = gameObject.Position;
                if(drawOnPixelGrid)
                {
                    worldPos = Vector2.Round(worldPos * pixelsPerWorldUnit) / pixelsPerWorldUnit;
                }
                if(gameObject.usesWorldPos)
                {
                    if(!camera.IsOnScreen(gameObject))
                        continue;

                    drawPos = camera.WorldPosToScreenPos(worldPos);
                    drawScale = gameObject.Scale * camera.zoom;
                }
                else
                {
                    drawPos = gameObject.Position;
                    drawScale = gameObject.Scale;
                }
                spriteBatch.Draw(
                    gameObject.Texture,
                    drawPos,
                    null,
                    gameObject.ColorMask,
                    gameObject.Rotation,
                    gameObject.TextureCenter, //origin, relative to top left corner of texture, before rotation and scale
                    drawScale,
                    SpriteEffects.None,
                    gameObject.zPos
                );
            }
            fps.DrawFps(spriteBatch, font, new Vector2(5f, 5f), Color.Black);
            spriteBatch.End();

            base.Draw(gameTime);
        }

        private void Iterate(Action<ManagedObject> action)
        {
            LockObjLists = true;
            foreach (GameObject gameObject in gameObjects)
            {
                action(gameObject);
            }
            foreach (ManagedObject managedObject in managedObjects)
            {
                action(managedObject);
            }
            LockObjLists = false;

            foreach (var (gameObject, parent) in objectsToAdd)
            {
                AddGameObject(gameObject, parent);
            }
            objectsToAdd.Clear();
            foreach (var gameObject in objectsToRemove)
            {
                RemoveGameObject(gameObject);
            }
            objectsToRemove.Clear();

            foreach (var managed in managedObjectsToAdd)
            {
                AddManagedObject(managed);
            }
            managedObjectsToAdd.Clear();
            foreach (var managed in managedObjectsToRemove)
            {
                RemoveManagedObject(managed);
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
            throw new ArgumentException();
        }
    }
}