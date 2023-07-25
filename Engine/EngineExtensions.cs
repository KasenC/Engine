using System;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Engine
{
    public static class EngineExtensions
    {
        public static Rect ToRect(this Rectangle rectangle)
        {
            return new Rect(rectangle.Location.ToVector2(), rectangle.Size.ToVector2());
        }
    }
}
