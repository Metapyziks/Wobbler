using System;

namespace Wobbler.Nodes
{
    public abstract class Filter : SingleOutputNode
    {
        public Input Input { get; set; }
    }

    public class LowPass : Filter
    {
        public Input CutoffFrequency { get; set; }

        [UpdateMethod]
        public static void Update(float input, float cutoffFrequency, float deltaTime, ref float output)
        {
            if (cutoffFrequency <= 0f)
            {
                output = 0f;
                return;
            }

            var rc = 1f / (MathF.PI * 2f * cutoffFrequency);
            var alpha = deltaTime / (rc + deltaTime);

            output += alpha * (input - output);
        }
    }

    public class Adsr : Filter
    {
        public Input Attack { get; set; }
        public Input Decay { get; set; }
        public Input Sustain { get; set; } = 1f;
        public Input Release { get; set; }

        private int State { get; set; } = 0;
        
        [UpdateMethod]
        public static void Update(float input,
            float attack, float decay, float sustain, float release,
            ref int state, float deltaTime, ref float output)
        {
            sustain = MathF.Min(MathF.Max(sustain, 0f), 1f);

            if (input > 0.5f)
            {
                switch (state)
                {
                    case 0:
                    {
                        // Attack
                        output += deltaTime / MathF.Max(attack, 0f);

                        if (output >= 1f)
                        {
                            output = 1f;
                            state = decay > 0f ? 1 : 2;
                        }

                        break;
                    }
                    case 1:
                    {
                        // Decay
                        output -= deltaTime * (1f - sustain) / MathF.Max(decay, 0f);

                        if (output <= sustain)
                        {
                            output = sustain;
                            state = 2;
                        }

                        break;
                    }
                    default:
                        // Sustain
                        output = sustain;
                        break;
                }
            }
            else
            {
                // Release
                state = 0;
                output -= deltaTime * sustain / MathF.Max(release, 0f);

                if (output < 0f)
                {
                    output = 0f;
                }
            }
        }
    }
}
