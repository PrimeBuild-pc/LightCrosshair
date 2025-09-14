using System;

namespace LightCrosshair
{
    public enum CompositeShapeType
    {
        CircleDot,
        CrossDot,
        CircleCross,
        CircleX
    }

    public sealed class ShapeDefaults
    {
        public int OuterSize { get; set; }
        public int OuterThickness { get; set; }
        public int OuterGapSize { get; set; }
        public int InnerSize { get; set; }
        public int InnerThickness { get; set; }
        public int InnerGapSize { get; set; }
    }

    public static class CompositeDefaults
    {
        public static ShapeDefaults? GetCompositeDefaults(CompositeShapeType shapeType)
        {
            switch (shapeType)
            {
                case CompositeShapeType.CircleDot:
                    return new ShapeDefaults
                    {
                        OuterSize = 45,
                        OuterThickness = 2,
                        OuterGapSize = 0,
                        InnerSize = 10,
                        InnerThickness = 0,
                        InnerGapSize = 0
                    };

                case CompositeShapeType.CrossDot:
                    return new ShapeDefaults
                    {
                        OuterSize = 45,
                        OuterThickness = 2,
                        OuterGapSize = 10,
                        InnerSize = 8,
                        InnerThickness = 0,
                        InnerGapSize = 0
                    };

                case CompositeShapeType.CircleCross:
                    // Keep existing user values; no strict defaults defined here
                    return null;

                case CompositeShapeType.CircleX:
                    // Keep existing user values; no strict defaults defined here
                    return null;
            }
            return null;
        }
    }
}
