using System;

namespace Wobbler.Nodes
{
    public class LowPass : SingleOutputNode
    {
        [Input]
        public Input CutoffFrequency { get; set; }

        [Input]
        public Input Input { get; set; }

        [Input]
        public Input LastOutput { get; set; }

        public LowPass()
        {
            LastOutput = Output;
        }

        public override void Update(in UpdateContext ctx)
        {
            var cutoffFrequency = ctx.Get(CutoffFrequency);
            var prev = ctx.Get(LastOutput);
            var next = ctx.Get(Input);

            var dt = (float)ctx.DeltaTime.Seconds;
            var rc = 1f / (MathF.PI * 2f * cutoffFrequency);
            var alpha = dt / (rc + dt);

            ctx.Set(Output, prev + alpha * (next - prev));
        }
    }
}
