using System;
using System.Collections.Generic;
using System.Linq;

namespace Wobbler
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class NextMethodAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DefineMethodAttribute : Attribute { }

    public abstract class Node
    {
        public static Node[] FindAllNodes(IEnumerable<Node> roots)
        {
            var queue = new Queue<Node>(roots);
            var set = new HashSet<Node>();

            while (queue.TryDequeue(out var next))
            {
                if (!set.Add(next)) continue;

                for (var i = 0; i < next.Type.InputCount; ++i)
                {
                    var input = next.GetInput(i);

                    if (input.ConnectedOutput.IsValid)
                    {
                        queue.Enqueue(input.ConnectedOutput.Node);
                    }
                }
            }

            return set.Reverse().ToArray();
        }

        public NodeType Type { get; }

        public Input GetInput(int index)
        {
            return (Input) Type.InputProperties[index].GetValue(this)!;
        }

        public void SetInput(int index, Input value)
        {
            Type.InputProperties[index].SetValue(this, value);
        }

        public Output GetOutput(int index) => new(this, index);

        protected Node()
        {
            Type = NodeType.Get(this);

            UpdateOutputs();
        }

        private void UpdateOutputs()
        {
            for (var i = 0; i < Type.OutputCount; ++i)
            {
                Type.OutputProperties[i].SetValue(this, GetOutput(i));
            }
        }
    }

    public abstract class SingleOutputNode : Node
    {
        public Output Output { get; set; }
    }
}
