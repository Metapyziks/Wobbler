using System;

namespace Wobbler.Nodes
{
    public abstract class Oscillator : SingleOutputNode
    {
        public Input Frequency { get; set; } = 1f;

        public Input Min { get; set; } = -1f;

        public Input Max { get; set; } = 1f;

        public float Phase { get; set; } = 0f;

        protected static void Update(float frequency, ref float phase, float deltaTime)
        {
            const float twoPi = MathF.PI * 2f;

            phase += deltaTime * frequency * twoPi;

            if (phase >= twoPi)
            {
                phase -= twoPi;
            }
        }
    }

    public class Sine : Oscillator
    {
        [NextMethod]
        public static void Update(float frequency, float min, float max,
            ref float phase, float deltaTime, out float output)
        {
            Update(frequency, ref phase, deltaTime);
            output = min + (max - min) * (MathF.Sin(phase) * 0.5f + 0.5f);
        }
    }

    public class Square : Oscillator
    {
        [NextMethod]
        public static void Update(float frequency, float min, float max,
            ref float phase, float deltaTime, out float output)
        {
            Update(frequency, ref phase, deltaTime);
            output = min + (max - min) * (phase <= MathF.PI ? 1f : 0f);
        }
    }
}
