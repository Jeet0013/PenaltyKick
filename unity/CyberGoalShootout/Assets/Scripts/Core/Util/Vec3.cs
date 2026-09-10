namespace CyberGoal.Core.Util
{
    /// <summary>
    /// A three-component vector that owes nothing to UnityEngine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unity ships a perfectly good <c>Vector3</c>, and using it here would cost
    /// the one property this assembly exists to have: the ability to run
    /// anywhere. The rules and the ball flight need to execute under
    /// <c>dotnet test</c> on a build machine, and — later — on a match server
    /// that has no graphics stack at all. A single <c>using UnityEngine;</c>
    /// takes both of those away.
    /// </para>
    /// <para>
    /// It is also a <c>readonly struct</c> of doubles rather than floats. The
    /// flight is integrated 120 times a second and a server has to reproduce a
    /// client's result exactly; float accumulation drifts differently depending
    /// on whether the JIT folds an operation into an FMA, and that difference is
    /// enough to turn a post into a goal. Doubles do not make this bit-exact on
    /// their own, but they push the error far below the 6 cm post radius that
    /// decides the argument.
    /// </para>
    /// </remarks>
    public readonly struct Vec3
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;

        public Vec3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static readonly Vec3 Zero = new Vec3(0, 0, 0);

        public double Magnitude => System.Math.Sqrt(X * X + Y * Y + Z * Z);

        /// <summary>Length in the XZ plane, ignoring height.</summary>
        public double HorizontalMagnitude => System.Math.Sqrt(X * X + Z * Z);

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Vec3 operator *(Vec3 a, double s) => new Vec3(a.X * s, a.Y * s, a.Z * s);

        public override string ToString() => $"({X:F3}, {Y:F3}, {Z:F3})";
    }

    /// <summary>Small helpers the Core needs and <c>System.Math</c> does not have.</summary>
    public static class MathHelp
    {
        public static double Clamp(double value, double min, double max)
            => value < min ? min : value > max ? max : value;

        public static double Lerp(double a, double b, double t) => a + (b - a) * t;
    }
}
