using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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
        public static Output Sin(Output frequency)
        {
            return new Sine
            {
                Frequency = frequency
            };
        }

        public static Output Sin(Output frequency, Output min, Output max)
        {
            return (max + min + new Sine
            {
                Frequency = frequency
            }.Output * (max - min)) * 0.5f;
        }

        public static Output Square(Output frequency)
        {
            return new Square
            {
                Frequency = frequency
            };
        }

        public static Output LowPass(Output signal, Output cutoffFrequency)
        {
            return new LowPass
            {
                Input = signal,
                CutoffFrequency = cutoffFrequency
            };
        }

        public static async Task PlayAsync(Output left, Output right, Time startTime, TimeSpan duration, int sampleRate = 44100)
        {
            if (!left.IsValid)
            {
                throw new ArgumentException("Invalid socket.", nameof(left));
            }

            if (!right.IsValid)
            {
                throw new ArgumentException("Invalid socket.", nameof(right));
            }

            var (indices, outputCount) = NodeIndices.CreateFromNodes(left.Node, right.Node);
            var totalSamples = (long) Math.Round(sampleRate * duration.Seconds);

            var a = new float[outputCount];
            var b = new float[outputCount];

            var leftIndices = indices.First(x => x.Node == left.Node);
            var rightIndices = indices.First(x => x.Node == right.Node);

            var leftIndex = leftIndices.GetOutputIndex(left);
            var rightIndex = rightIndices.GetOutputIndex(right);

            var time = startTime;
            var samplePeriod = TimeSpan.FromSamples(sampleRate, 1);

            var timer = new Stopwatch();
            timer.Start();

            const int chunkSampleCount = 8192;
            const int channelCount = 2;

            var chunkSamples = new float[chunkSampleCount * channelCount];
            var sampleProvider = new BufferedWaveProvider(WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channelCount));

            var byteSamples = new byte[chunkSampleCount * channelCount * sizeof(float)];

            using var output = new WaveOut();

            output.Init(sampleProvider);
            output.Play();

            var playing = false;

            for (var chunkStartIndex = 0L; chunkStartIndex < totalSamples; chunkStartIndex += chunkSampleCount)
            {
                var sampleCount = Math.Min((int) (totalSamples - chunkStartIndex), chunkSampleCount);

                if (sampleCount <= 0) break;

                for (var i = 0; i < sampleCount; ++i)
                {
                    foreach (var nodeIndices in indices)
                    {
                        var ctx = new UpdateContext(nodeIndices, samplePeriod, a, b);
                        nodeIndices.Node.Update(in ctx);
                    }

                    chunkSamples[(i << 1) + 0] = b[leftIndex];
                    chunkSamples[(i << 1) + 1] = b[rightIndex];

                    time += samplePeriod;

                    (a, b) = (b, a);
                }
                
                Buffer.BlockCopy(chunkSamples, 0, byteSamples, 0, sampleCount * channelCount * sizeof(float));
                sampleProvider.AddSamples(byteSamples, 0, byteSamples.Length);
            }

            await Task.Delay(duration);
        }
        
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

                property.SetValue(this, new Input(this, i, input.Value));
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
                outputCount += input.Value.Node?.FindAllNodes(outSet) ?? 0;
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
        public static (NodeIndices[] array, int outputCount) CreateFromNodes(params Node[] rootNodes)
        {
            var allNodes = new HashSet<Node>();
            var outputCount = 0;

            foreach (var node in rootNodes)
            {
                outputCount += node.FindAllNodes(allNodes);
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
                _inputIndices[i] = input.Value.IsValid
                    ? ctxArray[nodeDict[input.Value.Node]]._outputIndices[input.Value.Index]
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
            return input.Value.IsValid ? _prev[_nodeIndices.GetInputIndex(input)] : 0f;
        }

        public void Set(Output output, float value)
        {
            _next[_nodeIndices.GetOutputIndex(output)] = value;
        }
    }

    public readonly struct Input
    {
        public static implicit operator Input(Output output)
        {
            return new Input(null, 0, output);
        }

        public static implicit operator Input(float value)
        {
            return new Input(null, 0, value);
        }

        public bool IsValid => Node != null;

        internal Node Node { get; }

        internal int Index { get; }

        internal Output Value { get; }

        internal Input(Node node, int index, Output value)
        {
            Node = node;
            Index = index;
            Value = value;
        }
    }

    public readonly struct Output
    {
        public static implicit operator Output(float value)
        {
            return new Constant(value).Output;
        }

        public static implicit operator Output(SingleOutputNode node)
        {
            return node.Output;
        }

        public static Output operator +(Output a, Output b)
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

        public static Output operator *(Output a, Output b)
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
