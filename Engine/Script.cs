using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Engine
{
    public class Script : IManaged
    {
        public GameObject gameObject;

        public virtual void Initialize() { }
        
        public virtual void Update(GameTime gameTime) { }

        public virtual void DrawUpdate(GameTime gameTime) { }

        public virtual void UpdateBufferedVars () { }
    }
}
