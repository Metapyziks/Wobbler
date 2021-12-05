using System;
using System.Linq;
using NAudio.Wave;

namespace Wobbler
{
    public class InstrumentWaveProvider : IWaveProvider
    {
        public WaveFormat WaveFormat { get; }
        public IInstrument Instrument { get; }

        public float TimeScale { get; set; } = 1f;

        private readonly float _deltaTime;

        public InstrumentWaveProvider(int sampleRate, IInstrument instrument)
        {
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, instrument.OutputCount);
            Instrument = instrument;

            _deltaTime = (float)TimeSpan.FromSamples(sampleRate, 1d).Seconds;
        }

        public InstrumentWaveProvider(int sampleRate, params Output[] outputs)
        {
            if (outputs.Any(x => !x.IsValid))
            {
                throw new ArgumentException("All outputs must be valid.", nameof(outputs));
            }

            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, outputs.Length);

            _deltaTime = (float)TimeSpan.FromSamples(sampleRate, 1d).Seconds;

            var builder = new InstrumentBuilder(0);

            foreach (var output in outputs)
            {
                builder.AddOutput(output);
            }

            var ctor = builder.GenerateConstructor();

            Instrument = ctor();
        }

        public void Reset()
        {
            Instrument.Reset();
        }

        public void Next()
        {
            var globals = new GlobalParameters(_deltaTime * TimeScale);

            Instrument.Next(globals);
        }

        public float GetOutput(int index)
        {
            return Instrument.GetOutput(index);
        }

        [ThreadStatic]
        private static float[] _sOutputBuffer;

        private static float[] GetOutputBuffer(int minSize)
        {
            if (_sOutputBuffer != null && _sOutputBuffer.Length >= minSize)
            {
                return _sOutputBuffer;
            }

            var size = 64;

            while (size < minSize) size <<= 1;

            _sOutputBuffer = new float[size];

            return _sOutputBuffer;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            var channels = WaveFormat.Channels;
            var stride = channels * sizeof(float);
            var iters = count / stride;

            count = iters * stride;

            var outputBuffer = GetOutputBuffer(channels * iters);

            for (int i = 0, outIndex = 0; i < iters; ++i, outIndex += channels)
            {
                Next();

                for (var c = 0; c < channels; ++c)
                {
                    outputBuffer[outIndex + c] = Instrument.GetOutput(c);
                }
            }

            Buffer.BlockCopy(outputBuffer, 0, buffer, offset, count);

            return count;
        }
    }
}
