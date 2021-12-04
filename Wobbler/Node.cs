using System;
using System.Collections.Generic;

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
            Type.InputProperties[index].SetValue(this,
                value.ConnectedOutput.IsValid
                    ? new Input(this, index, value.ConnectedOutput)
                    : new Input(this, index, value.Constant));
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

        private void UpdateInputs()
        {
            for (var i = 0; i < Type.InputCount; ++i)
            {
                var property = Type.InputProperties[i];
                var input = (Input)property.GetValue(this)!;

                if (input.Node == this && input.Index == i) continue;

                SetInput(i, input);
            }
        }
    }

    public abstract class SingleOutputNode : Node
    {
        public Output Output { get; set; }
    }
}
