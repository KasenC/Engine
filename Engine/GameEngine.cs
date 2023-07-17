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
        public static float pixelsPerWorldUnit = 1f;

        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        
        protected Camera camera = new();
        SpriteFont font;

        private List<GameObject> gameObjects = new(), objectsToRemove = new();
        private List<Tuple<GameObject, GameObject>> objectsToAdd = new();
        private List<IManaged> managedObjects = new(), managedObjectsToRemove = new(), managedObjectsToAdd = new();

        //Execution status flags
        private bool execStatusInit = false, execStatusLoad = false, 
            LockObjLists = false;

        public bool enableFpsCounter = true;
        SimpleFps fps = new();

        public GameEngine()
        {
            _graphics = new GraphicsDeviceManager(this);
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
        public void AddManagedObject(IManaged managedObject)
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

        public void RemoveManagedObject(IManaged managedObject)
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
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            font = Content.Load<SpriteFont>("Arial10pt");
        }

        protected sealed override void BeginRun()
        {
            if (!execStatusInit)
                throw new InvalidOperationException("Engine.BeginRun() called before Engine.Initialize(). If Initialize() or LoadContent() are overridden, you MUST invoke the base implementations, e.g. base.Initialize();");
            else if (!execStatusLoad)
                throw new InvalidOperationException("Engine.BeginRun() called before Engine.LoadContent(). If LoadContent() is overridden, you MUST invoke the base implementation, e.g. base.LoadContent();");
        }

        protected sealed override void Update(GameTime gameTime)
        {
            Iterate((o) => o.Update(gameTime));
            fps.Update(gameTime);
            base.Update(gameTime);
        }

        protected sealed override void Draw(GameTime gameTime)
        {
            camera.SetWindowBounds(_graphics);

            Iterate((o) => o.DrawUpdate(gameTime));

            GraphicsDevice.Clear(Color.CornflowerBlue);
            _spriteBatch.Begin();
            foreach(var gameObject in gameObjects)
            {
                if (!gameObject.active || !gameObject.visible || gameObject.Texture == null)
                    continue;

                Vector2 drawPos, drawScale;
                if(gameObject.usesWorldPos)
                {
                    if(!camera.IsOnScreen(gameObject))
                        continue;

                    drawPos = camera.WorldPosToScreenPos(gameObject.Position);
                    drawScale = gameObject.Scale * camera.zoom;
                }
                else
                {
                    drawPos = gameObject.Position;
                    drawScale = gameObject.Scale;
                }
                _spriteBatch.Draw(
                    gameObject.Texture,
                    drawPos,
                    null,
                    Color.White,
                    gameObject.Rotation,
                    gameObject.TextureCenter, //origin, relative to top left corner of texture, before rotation and scale
                    drawScale,
                    SpriteEffects.None,
                    gameObject.zPos
                );
            }
            fps.DrawFps(_spriteBatch, font, new Vector2(5f, 5f), Color.Black);
            _spriteBatch.End();

            base.Draw(gameTime);
        }

        private void Iterate(Action<IManaged> action)
        {
            LockObjLists = true;
            foreach (GameObject gameObject in gameObjects)
            {
                action(gameObject);
            }
            foreach (IManaged managedObject in managedObjects)
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

    public class Camera
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
            Vector2 TLOffset = gameObject.ObjectCenter, BROffset = gameObject.Size - gameObject.ObjectCenter;
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

    public interface IManaged
    {
        public void Initialize();

        public void Update(GameTime gameTime);

        public void DrawUpdate(GameTime gameTime);
    }
}