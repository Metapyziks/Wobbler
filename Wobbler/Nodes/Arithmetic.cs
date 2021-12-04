namespace Wobbler.Nodes
{
    public class Constant : SingleOutputNode
    {
        public float Value { get; set; }

        public Constant(float value)
        {
            Value = value;
        }

        public override void Update(in UpdateContext ctx)
        {
            ctx.Set(Output, Value);
        }
    }

    public abstract class BinaryNode : SingleOutputNode
    {
        [Input]
        public Input Left { get; set; }

        [Input]
        public Input Right { get; set; }
    }

    public class Add : BinaryNode
    {
        public override void Update(in UpdateContext ctx)
        {
            ctx.Set(Output, ctx.Get(Left) + ctx.Get(Right));
        }
    }

    public class Subtract : BinaryNode
    {
        public override void Update(in UpdateContext ctx)
        {
            ctx.Set(Output, ctx.Get(Left) - ctx.Get(Right));
        }
    }

    public class Multiply : BinaryNode
    {
        public override void Update(in UpdateContext ctx)
        {
            ctx.Set(Output, ctx.Get(Left) * ctx.Get(Right));
        }
    }
}
