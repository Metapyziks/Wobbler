using System;
using System.Threading.Tasks;

namespace Wobbler.Examples
{
    using static Node;

    class Program
    {
        static async Task Main(string[] args)
        {
            var signal = LowPass(Square(440f) + Square(523.25f), Sin(0.5f, 100f, 1000f)) * 0.1f;
            var pan = Sin(1f, 0f, 1f);

            await PlayAsync(signal * pan, signal * (1f - pan), 0d, 5d);
        }
    }
}
