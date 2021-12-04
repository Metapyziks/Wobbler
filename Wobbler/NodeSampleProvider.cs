using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;

namespace Wobbler
{
    public class NodeSampleProvider : ISampleProvider
    {
        public WaveFormat WaveFormat { get; }

        public NodeSampleProvider(int sampleRate, params Output[] channels)
        {
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels.Length);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
