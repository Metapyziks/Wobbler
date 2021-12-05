using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;

namespace Wobbler
{
    public partial class Simulation : IWaveProvider
    {
        public WaveFormat WaveFormat { get; }
        public Output[] Outputs { get; }

        private readonly int[] _outputIndices;
        private readonly float[] _outputBuffer;

        private readonly float[] _values;

        private readonly float _deltaTime;
        private readonly NextDelegate _nextMethod;

        private readonly (int ValueIndex, Node Node, PropertyInfo Property)[] _stateProperties;

        public Simulation(int sampleRate, params Output[] outputs)
        {
            if (outputs.Any(x => !x.IsValid))
            {
                throw new ArgumentException("All outputs must be valid.", nameof(outputs));
            }

            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, outputs.Length);
            Outputs = outputs;

            _deltaTime = (float)TimeSpan.FromSamples(sampleRate, 1d).Seconds;

            var nodes = FindAllNodes(outputs.Select(x => x.Node));
            var indices = AssignValueIndices(nodes);

            _nextMethod = GenerateNextMethod(nodes, indices);

            _outputIndices = outputs
                .Select(x => indices[(x.Node, x.Index)])
                .ToArray();

            _values = new float[indices.Count];
            _outputBuffer = new float[outputs.Length];

            _stateProperties = nodes
                .SelectMany(x => x.Type.UpdateMethodParameters
                    .Where(y => y.Type == UpdateParameterType.State)
                    .Select(y => (indices[(x, x.Type.OutputCount + y.Index)], x, y.Property)))
                .ToArray();

            Reset();
        }

        public void Reset()
        {
            Array.Clear(_values, 0, _values.Length);

            foreach (var item in _stateProperties)
            {
                _values[item.ValueIndex] = (float) Convert.ChangeType(item.Property.GetValue(item.Node), typeof(float))!;
            }
        }

        private static Node[] FindAllNodes(IEnumerable<Node> roots)
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

        public void Next()
        {
            var specialParams = new SpecialParameters(_deltaTime);

            _nextMethod(_values, specialParams);
        }

        public float GetOutput(int index)
        {
            return _values[_outputIndices[index]];
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            var stride = Outputs.Length * sizeof(float);

            count = count / stride * stride;

            for (var i = 0; i < count; i += stride)
            {
                Next();

                for (var c = 0; c < Outputs.Length; ++c)
                {
                    _outputBuffer[c] = GetOutput(c);
                }

                Buffer.BlockCopy(_outputBuffer, 0, buffer, offset + i, stride);
            }

            return count;
        }
    }
}
