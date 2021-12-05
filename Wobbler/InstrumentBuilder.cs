using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Wobbler
{
    public interface IInstrument
    {
        int InputCount { get; }
        int OutputCount { get; }

        void Next(GlobalParameters globals);
        void Reset();

        void SetInput(int index, float value);
        float GetOutput(int index);
    }

    public class Instrument : Node
    {

    }

    public delegate IInstrument InstrumentCtor();

    public class InstrumentBuilder
    {
        private enum ValueKind
        {
            Output,
            State
        }

        private readonly struct ValueInfo
        {
            public bool IsLocal { get; }
            public bool IsField => !IsLocal;

            public string Name { get; }
            public Type Type { get; }

            public ValueInfo(bool isLocal, string name, Type type)
            {
                IsLocal = isLocal;
                Name = name;
                Type = type;
            }
        }

        public class Input : SingleOutputNode { }

        public int InputCount => Inputs.Count;

        private readonly Input[] _inputs;
        private readonly List<Output> _outputs = new List<Output>();

        public int OutputCount => _outputs.Count;

        public IReadOnlyList<Input> Inputs => _inputs;
        public IReadOnlyList<Output> Outputs => _outputs;

        public InstrumentBuilder(int inputCount)
        {
            _inputs = new Input[inputCount];
        }

        public void AddOutput(Output output)
        {
            _outputs.Add(output);
        }

        public InstrumentCtor GenerateConstructor()
        {
            var values = new Dictionary<(Node Node, ValueKind Kind, int Index), ValueInfo>();
            var nodes = Node.FindAllNodes(Outputs.Select(x => x.Node));

            var processed = new HashSet<Node>();

            void RegisterOutput(Output value, bool canBeLocal)
            {
                var key = (value.Node, ValueKind.Output, value.Index);

                if (values.TryGetValue(key, out var existing))
                {
                    if (canBeLocal || existing.IsField) return;

                    values[key] = new ValueInfo(false, existing.Name, typeof(float));
                    return;
                }

                values.Add(key, new ValueInfo(canBeLocal, $"Output{values.Count}", typeof(float)));
            }

            void RegisterState(Node node, int index, Type type)
            {
                var key = (node, ValueKind.State, index);

                if (!values.ContainsKey(key))
                {
                    values.Add(key, new ValueInfo(false, $"State{values.Count}", type));
                }
            }

            foreach (var node in nodes)
            {
                foreach (var parameter in node.Type.UpdateMethodParameters)
                {
                    switch (parameter.Type)
                    {
                        case UpdateParameterType.Input:
                            var input = node.GetInput(parameter.Index);

                            if (input.ConnectedOutput.IsValid)
                            {
                                RegisterOutput(input.ConnectedOutput, processed.Contains(input.ConnectedOutput.Node));
                            }

                            break;

                        case UpdateParameterType.Output:
                            // If parameter is ref rather than out, we need
                            // to store the last value as a field
                            if (!parameter.Parameter.IsOut)
                            {
                                var output = node.GetOutput(parameter.Index);
                                RegisterOutput(output, false);
                            }

                            break;

                        case UpdateParameterType.State:
                            RegisterState(node, parameter.Index, parameter.Property.PropertyType);
                            break;
                    }
                }

                processed.Add(node);
            }

            foreach (var output in Outputs)
            {
                RegisterOutput(output, false);
            }

            throw new NotImplementedException();
        }

        private DynamicMethod GenerateNextMethod(Node[] nodes, Dictionary<(Node Node, int Index), int> indices)
        {
            var paramTypes = new[]
            {
                typeof(float[]),
                typeof(GlobalParameters)
            };

            var method = new DynamicMethod("Next", typeof(void), paramTypes, typeof(Node).Module);

            var ilGen = method.GetILGenerator();

            var locals = new Dictionary<int, LocalBuilder>();

            foreach (var node in nodes)
            {
                locals.Clear();

                foreach (var parameter in node.Type.UpdateMethodParameters)
                {
                    if (parameter.Type != UpdateParameterType.State) continue;
                    if (!parameter.Parameter.ParameterType.IsByRef) continue;
                    if (parameter.Property.PropertyType == typeof(float)) continue;

                    var index = indices[(node, node.Type.OutputCount + parameter.Index)];
                    var local = ilGen.DeclareLocal(parameter.Property.PropertyType);

                    locals.Add(index, local);

                    ilGen.Emit(OpCodes.Ldarg_0);
                    ilGen.Emit(OpCodes.Ldc_I4, index);
                    ilGen.Emit(OpCodes.Ldelem_R4);

                    if (parameter.Property.PropertyType == typeof(int))
                    {
                        ilGen.Emit(OpCodes.Conv_I4);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }

                    ilGen.Emit(OpCodes.Stloc, local);
                }

                foreach (var parameter in node.Type.UpdateMethodParameters)
                {
                    switch (parameter.Type)
                    {
                        case UpdateParameterType.Input:
                            var input = node.GetInput(parameter.Index);

                            if (input.ConnectedOutput.IsValid)
                            {
                                ilGen.Emit(OpCodes.Ldarg_0);
                                ilGen.Emit(OpCodes.Ldc_I4, indices[input.ConnectedOutput]);
                                ilGen.Emit(OpCodes.Ldelem_R4);
                            }
                            else
                            {
                                ilGen.Emit(OpCodes.Ldc_R4, input.Constant);
                            }
                            break;

                        case UpdateParameterType.Output:
                            var output = node.GetOutput(parameter.Index);

                            ilGen.Emit(OpCodes.Ldarg_0);
                            ilGen.Emit(OpCodes.Ldc_I4, indices[output]);
                            ilGen.Emit(OpCodes.Ldelema, typeof(float));
                            break;

                        case UpdateParameterType.State:
                            var index = indices[(node, node.Type.OutputCount + parameter.Index)];

                            if (locals.TryGetValue(index, out var local))
                            {
                                ilGen.Emit(OpCodes.Ldloca_S, local);
                                break;
                            }

                            ilGen.Emit(OpCodes.Ldarg_0);
                            ilGen.Emit(OpCodes.Ldc_I4, index);

                            if (parameter.Property.PropertyType == typeof(float))
                            {
                                if (parameter.Parameter.ParameterType.IsByRef)
                                {
                                    ilGen.Emit(OpCodes.Ldelema, typeof(float));
                                }
                                else
                                {
                                    ilGen.Emit(OpCodes.Ldelem_R4);
                                }

                                break;
                            }

                            ilGen.Emit(OpCodes.Ldelem_R4);

                            if (parameter.Property.PropertyType == typeof(int))
                            {
                                ilGen.Emit(OpCodes.Conv_I4);
                            }
                            else
                            {
                                throw new NotImplementedException();
                            }
                            break;

                        case UpdateParameterType.Global:
                            ilGen.Emit(OpCodes.Ldarga_S, (byte)1);
                            ilGen.Emit(OpCodes.Call, parameter.Property.GetMethod);
                            break;
                    }
                }

                ilGen.Emit(OpCodes.Call, node.Type.UpdateMethod);

                foreach (var parameter in node.Type.UpdateMethodParameters)
                {
                    if (parameter.Type != UpdateParameterType.State) continue;

                    var index = indices[(node, node.Type.OutputCount + parameter.Index)];

                    if (!locals.TryGetValue(index, out var local)) continue;

                    ilGen.Emit(OpCodes.Ldarg_0);
                    ilGen.Emit(OpCodes.Ldc_I4, index);
                    ilGen.Emit(OpCodes.Ldloc, local);
                    ilGen.Emit(OpCodes.Conv_R4);
                    ilGen.Emit(OpCodes.Stelem_R4);
                }
            }

            ilGen.Emit(OpCodes.Ret);

            return method;
        }
    }
}
