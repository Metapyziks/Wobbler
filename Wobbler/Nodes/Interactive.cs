using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Wobbler.Nodes
{
    public class KeyInput : SingleOutputNode
    {
        private static bool _sAttachedEvents;

        private static readonly HashSet<Key> _sPressedKeys = new HashSet<Key>();

        public Key Key { get; set; }

        public override void Update(in UpdateContext ctx)
        {
            // TODO

            ctx.Set(Output, _sPressedKeys.Contains(Key) ? 1f : 0f);
        }
    }
}
