using System;

namespace Wobbler.Nodes
{
    public abstract class Filter : SingleOutputNode
    {
        [Input]
        public Input Input { get; set; }
    }

    public class LowPass : Filter
    {
        [Input]
        public Input CutoffFrequency { get; set; }

        public override void Update(in UpdateContext ctx)
        {
            var cutoffFrequency = ctx.Get(CutoffFrequency);
            var prev = ctx.Get(Output);
            var next = ctx.Get(Input);

            var dt = (float)ctx.DeltaTime.Seconds;
            var rc = 1f / (MathF.PI * 2f * cutoffFrequency);
            var alpha = dt / (rc + dt);

            ctx.Set(Output, prev + alpha * (next - prev));
        }
    }

    public class Adsr : Filter
    {
        [Input]
        public Input Attack { get; set; }

        [Input]
        public Input Decay { get; set; }

        [Input]
        public Input Sustain { get; set; }

        [Input]
        public Input Release { get; set; }

        [Output] public Output State => GetOutput(1);

        public override void Update(in UpdateContext ctx)
        {
            var input = ctx.Get(Input);

            var attack = MathF.Max(ctx.Get(Attack), 0f);
            var decay = MathF.Max(ctx.Get(Decay), 0f);
            var sustain = MathF.Max(MathF.Min(ctx.Get(Sustain), 1f), 0f);
            var release = MathF.Max(ctx.Get(Release), 0f);

            var output = ctx.Get(Output);
            var state = (int) ctx.Get(State);

            var dt = (float)ctx.DeltaTime.Seconds;

            if (input > 0.5f)
            {
                switch (state)
                {
                    case 0:
                    {
                        // Attack
                        output += dt / attack;

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
                        output -= dt * (1f - sustain) / decay;

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
                state = 0;
                output -= dt * sustain / release;

                if (output < 0f)
                {
                    output = 0f;
                }
            }

            ctx.Set(Output, output);
            ctx.Set(State, state);
        }
    }
}
