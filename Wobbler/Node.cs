using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Input;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Wobbler.Nodes;

namespace Wobbler
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class InputAttribute : Attribute
    {

    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class OutputAttribute : Attribute
    {

    }

    public abstract class Node
    {
        private static readonly Dictionary<Type, PropertyInfo[]> _sInputCache = new();
        private static readonly Dictionary<Type, PropertyInfo[]> _sOutputCache = new();

        private static PropertyInfo[] GetProperties<TValue, TAttrib>(Type type, Dictionary<Type, PropertyInfo[]> cache)
            where TAttrib : Attribute
        {
            if (cache.TryGetValue(type, out var properties)) return properties;

            properties = type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(x => x.PropertyType == typeof(TValue))
                .Where(x => x.CanRead && x.CanWrite == (typeof(TValue) == typeof(Input)))
                .Where(x => x.GetCustomAttribute<TAttrib>() != null)
                .ToArray();

            cache.Add(type, properties);

            return properties;
        }

        private readonly PropertyInfo[] _inputProperties;
        private readonly PropertyInfo[] _outputProperties;

        public int InputCount => _inputProperties.Length;
        public int OutputCount => _outputProperties.Length;

        public Input GetInput(int index)
        {
            return (Input) _inputProperties[index].GetValue(this)!;
        }

        public Output GetOutput(int index) => new(this, index);

        protected Node()
        {
            _inputProperties = GetProperties<Input, InputAttribute>(GetType(), _sInputCache);
            _outputProperties = GetProperties<Output, OutputAttribute>(GetType(), _sOutputCache);
        }

        private void UpdateInputs()
        {
            for (var i = 0; i < _inputProperties.Length; ++i)
            {
                var property = _inputProperties[i];
                var input = (Input)property.GetValue(this)!;

                if (input.Node == this && input.Index == i) continue;

                property.SetValue(this, input.Signal.IsValid
                    ? new Input(this, i, input.Signal)
                    : new Input(this, i, input.Constant));
            }
        }

        public abstract void Update(in UpdateContext ctx);

        public int FindAllNodes(HashSet<Node> outSet)
        {
            if (!outSet.Add(this)) return 0;

            UpdateInputs();

            var outputCount = OutputCount;

            for (var i = 0; i < InputCount; ++i)
            {
                var input = GetInput(i);
                outputCount += input.Signal.Node?.FindAllNodes(outSet) ?? 0;
            }

            return outputCount;
        }
    }

    public abstract class SingleOutputNode : Node
    {
        [Output] public Output Output => GetOutput(0);
    }

    public readonly struct NodeIndices
    {
        public static (NodeIndices[] array, int outputCount) CreateFromOutputs(params Output[] outputs)
        {
            var allNodes = new HashSet<Node>();
            var outputCount = 0;

            foreach (var output in outputs)
            {
                if (output.Node == null) continue;

                outputCount += output.Node.FindAllNodes(allNodes);
            }

            var array = allNodes.Select(x => new NodeIndices(x))
                .ToArray();
            var nodeDict = new Dictionary<Node, int>();

            var nextOutputIndex = 0;

            for (var nodeIndex = 0; nodeIndex < array.Length; ++nodeIndex)
            {
                var ctx = array[nodeIndex];

                nodeDict.Add(ctx.Node, nodeIndex);
                
                ctx.AssignOutputIndices(ref nextOutputIndex);
            }

            foreach (var ctx in array)
            {
                ctx.UpdateInputIndices(array, nodeDict);
            }

            return (array, outputCount);
        }

        public Node Node { get; }

        private readonly int[] _inputIndices;
        private readonly int[] _outputIndices;

        private NodeIndices(Node node)
        {
            Node = node;

            _inputIndices = new int[node.InputCount];
            _outputIndices = new int[node.OutputCount];
        }

        internal int GetInputIndex(Input input)
        {
            if (input.Node != Node) throw new ArgumentException("Input node doesn't match the currently updating node.");

            return _inputIndices[input.Index];
        }

        internal int GetOutputIndex(Output output)
        {
            if (output.Node != Node) throw new ArgumentException("Output node doesn't match the currently updating node.");

            return _outputIndices[output.Index];
        }

        private void AssignOutputIndices(ref int nextIndex)
        {
            for (var i = 0; i < _outputIndices.Length; ++i)
            {
                _outputIndices[i] = nextIndex++;
            }
        }

        private void UpdateInputIndices(NodeIndices[] ctxArray, Dictionary<Node, int> nodeDict)
        {
            for (var i = 0; i < _inputIndices.Length; ++i)
            {
                var input = Node.GetInput(i);
                _inputIndices[i] = input.Signal.IsValid
                    ? ctxArray[nodeDict[input.Signal.Node]]._outputIndices[input.Signal.Index]
                    : -1;
            }
        }
    }

    public readonly struct UpdateContext
    {
        private readonly NodeIndices _nodeIndices;

        private readonly float[] _prev;
        private readonly float[] _next;

        public TimeSpan DeltaTime { get; }

        public UpdateContext(NodeIndices nodeIndices, TimeSpan dt, float[] prev, float[] next)
        {
            _nodeIndices = nodeIndices;

            DeltaTime = dt;

            _prev = prev;
            _next = next;
        }

        public float Get(Input input)
        {
            return input.Signal.IsValid ? _prev[_nodeIndices.GetInputIndex(input)] : input.Constant;
        }

        public float Get(Output output)
        {
            return _prev[_nodeIndices.GetOutputIndex(output)];
        }

        public void Set(Output output, float value)
        {
            _next[_nodeIndices.GetOutputIndex(output)] = value;
        }
    }

    public readonly struct Input
    {
        public static implicit operator Input(SingleOutputNode node)
        {
            return new Input(null, 0, node.Output);
        }

        public static implicit operator Input(Output output)
        {
            return new Input(null, 0, output);
        }

        public static implicit operator Input(float value)
        {
            return new Input(null, 0, value);
        }

        public static implicit operator Input(Key key)
        {
            return new Input(null, 0, key);
        }

        public bool IsValid => Node != null;

        internal Node Node { get; }

        internal int Index { get; }

        internal Output Signal { get; }

        internal float Constant { get; }

        internal Input(Node node, int index, Output signal)
        {
            Node = node;
            Index = index;
            Signal = signal;
            Constant = 0f;
        }

        internal Input(Node node, int index, float constant)
        {
            Node = node;
            Index = index;
            Signal = default;
            Constant = constant;
        }
    }

    public readonly struct Output
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
    }
}
