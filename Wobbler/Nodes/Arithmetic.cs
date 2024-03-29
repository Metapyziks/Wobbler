﻿namespace Wobbler.Nodes
{
    public class Constant : SingleOutputNode
    {
        public float Value { get; set; }

        [NextMethod]
        public static void Next(float value, out float output)
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
        [NextMethod]
        public static void Next(float left, float right, out float output)
        {
            output = left + right;
        }
    }

    public class Subtract : BinaryNode
    {
        [NextMethod]
        public static void Next(float left, float right, out float output)
        {
            output = left - right;
        }
    }

    public class Multiply : BinaryNode
    {
        [NextMethod]
        public static void Next(float left, float right, out float output)
        {
            output = left * right;
        }
    }
}
