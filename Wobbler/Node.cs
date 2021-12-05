using System;

namespace Wobbler
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class UpdateMethodAttribute : Attribute { }

    public abstract partial class Node
    {
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
