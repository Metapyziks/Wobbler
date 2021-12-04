using System;
using System.Collections.Generic;
using System.Linq;
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

        private float[] _values;

        private readonly float _deltaTime;

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
            var valueIndices = new Dictionary<(Node Node, int Index), int>();

            int GetValueIndex(Node node, int index)
            {
                if (valueIndices.TryGetValue((node, index), out var value))
                {
                    return value;
                }

                value = valueIndices.Count;
                valueIndices.Add((node, index), value);

                return value;
            }

            int GetOutputIndex(Output output) => GetValueIndex(output.Node, output.Index);
            int GetStateIndex(Node node, int index) => GetValueIndex(node, node.Type.OutputCount + index);

            foreach (var node in nodes)
            {
                Console.WriteLine($"{node.Type.UpdateMethod.DeclaringType!.FullName}.{node.Type.UpdateMethod.Name}(");

                foreach (var parameter in node.Type.UpdateMethodParameters)
                {
                    switch (parameter.Type)
                    {
                        case UpdateParameterType.Input:
                            var input = node.GetInput(parameter.Index);

                            if (input.ConnectedOutput.IsValid)
                            {
                                Console.WriteLine($"  {parameter.Parameter.Name}: in [{GetOutputIndex(input.ConnectedOutput)}],");
                            }
                            else
                            {
                                Console.WriteLine($"  {parameter.Parameter.Name}: {input.Constant},");
                            }
                            break;

                        case UpdateParameterType.Output:
                            var output = node.GetOutput(parameter.Index);

                            if (parameter.Parameter.IsOut)
                            {
                                Console.WriteLine($"  {parameter.Parameter.Name}: out [{GetOutputIndex(output)}],");
                            }
                            else
                            {
                                Console.WriteLine($"  {parameter.Parameter.Name}: ref [{GetOutputIndex(output)}],");
                            }
                            break;

                        case UpdateParameterType.State:
                            if (parameter.Parameter.ParameterType.IsByRef)
                            {
                                Console.WriteLine($"  {parameter.Parameter.Name}: ref [{GetStateIndex(node, parameter.Index)}],");
                            }
                            else
                            {
                                Console.WriteLine($"  {parameter.Parameter.Name}: in [{GetStateIndex(node, parameter.Index)}],");
                            }
                            break;

                        case UpdateParameterType.Special:
                            Console.WriteLine($"  {parameter.Parameter.Name}: in {parameter.Property.Name},");
                            break;
                    }
                }
                
                Console.WriteLine(")");
            }

            _values = new float[valueIndices.Count];
            _outputBuffer = new float[outputs.Length];
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
            throw new NotImplementedException();
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
