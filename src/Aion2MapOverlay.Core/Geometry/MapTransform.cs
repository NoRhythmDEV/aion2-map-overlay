namespace Aion2MapOverlay.Core.Geometry;

// Reference-image pixels to captured-frame pixels; map artwork does not rotate.
public readonly record struct MapTransform(double Scale, double X, double Y)
{
    public (double X, double Y) Apply(double x, double y) => (x * Scale + X, y * Scale + Y);
    public (double X, double Y) Inverse(double x, double y) => ((x - X) / Scale, (y - Y) / Scale);
    public MapTransform Then(MapTransform next) => new(Scale * next.Scale, X * next.Scale + next.X, Y * next.Scale + next.Y);
}
