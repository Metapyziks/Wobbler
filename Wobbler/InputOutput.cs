using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Wobbler.Nodes;

namespace Wobbler
{
    public readonly struct Input
    {
        public static implicit operator Input(SingleOutputNode node)
        {
            return new Input(node.Output);
        }

        public static implicit operator Input(Output output)
        {
            return new Input(output);
        }

        public static implicit operator Input(float value)
        {
            return new Input(value);
        }

        public static implicit operator Input(Key key)
        {
            return new Input(key);
        }
        
        internal Output ConnectedOutput { get; }

        internal float Constant { get; }

        internal Input(Output signal)
        {
            ConnectedOutput = signal;
            Constant = 0f;
        }

        internal Input(float constant)
        {
            ConnectedOutput = default;
            Constant = constant;
        }
    }

    public readonly struct Output : IEquatable<Output>
    {
        public static implicit operator Output(SingleOutputNode node)
        {
            return node.Output;
        }

        public static implicit operator Output(Key key)
        {
            return new KeyInput
            {
                Key = key
            }.Output;
        }

        public static implicit operator (Node Node, int Index)(Output output)
        {
            return (output.Node, output.Index);
        }

        public static Output operator +(Output a, Output b)
        {
            return new Add
            {
                Left = a,
                Right = b
            };
        }

        public static Output operator +(Output a, float b)
        {
            return new Add
            {
                Left = a,
                Right = b
            };
        }

        public static Output operator +(float a, Output b)
        {
            return new Add
            {
                Left = a,
                Right = b
            };
        }

        public static Output operator -(Output a, Output b)
        {
            return new Subtract
            {
                Left = a,
                Right = b
            };
        }

        public static Output operator -(Output a, float b)
        {
            return new Subtract
            {
                Left = a,
                Right = b
            };
        }

        public static Output operator -(float a, Output b)
        {
            return new Subtract
            {
                Left = a,
                Right = b
            };
        }

        public static Output operator *(Output a, Output b)
        {
            return new Multiply
            {
                Left = a,
                Right = b
            };
        }

        public static Output operator *(Output a, float b)
        {
            return new Multiply
            {
                Left = a,
                Right = b
            };
        }

        public static Output operator *(float a, Output b)
        {
            return new Multiply
            {
                Left = a,
                Right = b
            };
        }

        public bool IsValid => Node != null;

        internal Node Node { get; }

        internal int Index { get; }

        internal Output(Node node, int index)
        {
            Node = node;
            Index = index;
        }

        public bool Equals(Output other)
        {
            return Equals(Node, other.Node) && Index == other.Index;
        }

        public override bool Equals(object obj)
        {
            return obj is Output other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Node, Index);
        }
    }
}
