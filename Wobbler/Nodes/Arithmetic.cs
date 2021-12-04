namespace Wobbler.Nodes
{
    public class Constant : SingleOutputNode
    {
        public float Value { get; set; }

        [UpdateMethod]
        public static void Update(float value, out float output)
        {
            output = value;
        }
    }

    public abstract class BinaryNode : SingleOutputNode
    {
        public Input Left { get; set; }
        public Input Right { get; set; }
    }

    public class Add : BinaryNode
    {
        [UpdateMethod]
        public static void Update(float left, float right, out float output)
        {
            output = left + right;
        }
    }

    public class Subtract : BinaryNode
    {
        [UpdateMethod]
        public static void Update(float left, float right, out float output)
        {
            output = left - right;
        }
    }

    public class Multiply : BinaryNode
    {
        [UpdateMethod]
        public static void Update(float left, float right, out float output)
        {
            output = left * right;
        }
    }
}
