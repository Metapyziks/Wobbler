using System.Threading.Tasks;
using System.Windows.Input;
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
                    Frequency = 10f,
                    Min = 10f,
                    Max = 1000f
                }
            }.Output * 0.1f;

            var envelope = new Adsr
            {
                Input = new Square
                {
                    Frequency = 3f
                },

                Attack = 0.025f,
                Decay = 0.05f,
                Release = 0.25f,
                Sustain = 0.5f
            }.Output;

            var pan = new Sine
            {
                Frequency = 1f,
                Min = 0f,
                Max = 1f
            }.Output;

            using var output = new WaveOut();

            output.Init(new NodeSampleProvider(44100, envelope * signal * pan, envelope * signal * (1f - pan)));
            output.Play();

            await Task.Delay(TimeSpan.FromSeconds(60d));
        }
    }
}
