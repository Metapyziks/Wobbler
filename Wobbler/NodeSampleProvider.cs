using System;
using System.Linq;
using NAudio.Wave;
using Wobbler.Nodes;

namespace Wobbler
{
    public class NodeSampleProvider : IWaveProvider
    {
        public WaveFormat WaveFormat { get; }

        private readonly NodeIndices[] _nodeIndices;
        private readonly NodeIndices[] _updateList;
        private readonly int[] _channelIndices;

        private readonly float[] _channelsBuffer;

        private float[] _prev;
        private float[] _next;

        private readonly TimeSpan _deltaTime;

        public NodeSampleProvider(int sampleRate, params Output[] channels)
        {
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels.Length);

            int outputCount;

            (_nodeIndices, outputCount) = NodeIndices.CreateFromOutputs(channels);

            _channelsBuffer = new float[channels.Length];

            _prev = new float[outputCount];
            _next = new float[outputCount];

            _channelIndices = channels
                .Select(x => _nodeIndices
                    .First(y => y.Node == x.Node)
                    .GetOutputIndex(x))
                .ToArray();

            _deltaTime = TimeSpan.FromSamples(sampleRate, 1d);
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            var stride = _channelsBuffer.Length * sizeof(float);

            for (var i = 0; i + stride <= count; i += stride)
            {
                foreach (var indices in _nodeIndices)
                {
                    var ctx = new UpdateContext(indices, _deltaTime, _prev, _next);
                    indices.Node.Update(in ctx);
                }

                for (var c = 0; c < _channelIndices.Length; ++c)
                {
                    _channelsBuffer[c] = _next[_channelIndices[c]];
                }

                Buffer.BlockCopy(_channelsBuffer, 0, buffer, offset + i, stride);

                (_prev, _next) = (_next, _prev);
            }

            return count;
        }
    }
}
