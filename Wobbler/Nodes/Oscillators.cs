using System;

namespace Wobbler.Nodes
{
    public abstract class Oscillator : SingleOutputNode
    {
        [Input]
        public Input Frequency { get; set; } = 1f;

        [Input] public Input LastPhase { get; set; }

        [Output] public Output NextPhase => GetOutput(1);
        
        protected Oscillator()
        {
            LastPhase = NextPhase;
        }

        public override void Update(in UpdateContext ctx)
        {
            const float twoPi = MathF.PI * 2f;

            var phase = ctx.Get(LastPhase) + (float)ctx.DeltaTime.Seconds * ctx.Get(Frequency) * twoPi;

            if (phase >= twoPi)
            {
                phase -= twoPi;
            }

            ctx.Set(NextPhase, phase);
            ctx.Set(Output, GetAmplitude(phase));
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
