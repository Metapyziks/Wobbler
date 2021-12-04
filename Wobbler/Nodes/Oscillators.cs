using System;

namespace Wobbler.Nodes
{
    public abstract class Oscillator : SingleOutputNode
    {
        [Input]
        public Input Frequency { get; set; } = 1f;

        [Input] public Input Min { get; set; } = -1f;

        [Input] public Input Max { get; set; } = 1f;

        [Output] public Output Phase => GetOutput(1);

        public override void Update(in UpdateContext ctx)
        {
            const float twoPi = MathF.PI * 2f;

            var phase = ctx.Get(Phase) + (float)ctx.DeltaTime.Seconds * ctx.Get(Frequency) * twoPi;
            var min = ctx.Get(Min);
            var max = ctx.Get(Max);

            if (phase >= twoPi)
            {
                phase -= twoPi;
            }

            ctx.Set(Phase, phase);
            ctx.Set(Output, (min + max + GetAmplitude(phase) * (max - min)) * 0.5f);
        }

        protected abstract float GetAmplitude(float phase);
    }

    public class Sine : Oscillator
    {
        protected override float GetAmplitude(float phase)
        {
            return MathF.Sin(phase);
        }
    }

    public class Square : Oscillator
    {
        protected override float GetAmplitude(float phase)
        {
            return phase <= MathF.PI ? 1f : -1f;
        }
    }
}
