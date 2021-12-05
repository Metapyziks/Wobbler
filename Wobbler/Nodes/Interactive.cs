using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Wobbler.Nodes
{
    public class KeyInput : SingleOutputNode
    {
        public Key Key { get; set; }

        [NextMethod]
        public static void Update(Key key, out float output)
        {
            output = 0f;
        }
    }
}
