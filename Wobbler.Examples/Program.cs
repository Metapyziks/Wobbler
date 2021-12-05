using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;
using Wobbler.Nodes;

namespace Wobbler.Examples
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var freq = new Square
            {
                Frequency = 0.25f,
                Min = 440f,
                Max = 523.25f
            }.Output;

            var signal = new LowPass
            {
                Input = new Sine
                {
                    Frequency = freq
                },
                CutoffFrequency = new Sine
                {
                    Frequency = 5f,
                    Min = 10f,
                    Max = 1000f
                }
            }.Output * new Adsr
            {
                Input = new Square
                {
                    Frequency = 3f
                },

                Attack = 0.025f,
                Decay = 0.05f,
                Sustain = 0.5f,
                Release = 0.25f
            }.Output * 0.1f;

            var pan = new Sine
            {
                Frequency = 1f,
                Min = 0f,
                Max = 1f
            }.Output;

            var sim = new Simulation(44100,
                signal * pan,
                signal * (1f - pan));

            await using var writer = new WaveFileWriter("test.wav", sim.WaveFormat);

            var buffer = new byte[65536];
            var stride = sizeof(float) * 2;
            var iters = 44100 * 30 * stride / buffer.Length;

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            for (var i = 0; i < iters; ++i)
            {
                sim.Read(buffer, 0, buffer.Length);
                writer.Write(buffer, 0, buffer.Length);
            }

            stopwatch.Stop();

            var sampleCount = iters * buffer.Length / stride;

            Console.WriteLine($"Generated {sampleCount:N0} samples in {stopwatch.Elapsed.TotalMilliseconds:F3}ms ({stopwatch.Elapsed.TotalMilliseconds * 1000d / sampleCount:F3}μs per sample)");

            using var output = new WaveOut();

            output.Init(sim);
            output.Play();

            await Task.Delay(TimeSpan.FromSeconds(60d));
        }
    }
}
