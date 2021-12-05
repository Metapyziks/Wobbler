using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Wobbler
{
    partial class Simulation
    {
        private delegate void NextDelegate(float[] values, SpecialParameters specialParams);

        private Dictionary<(Node Node, int Index), int> AssignValueIndices(Node[] nodes)
        {
            var valueIndices = new Dictionary<(Node Node, int Index), int>();

            void AddValueIndex(Node node, int index)
            {
                if (valueIndices.ContainsKey((node, index))) return;

                valueIndices.Add((node, index), valueIndices.Count);
            }

            void AddOutputIndex(Output output) => AddValueIndex(output.Node, output.Index);
            void AddStateIndex(Node node, int index) => AddValueIndex(node, node.Type.OutputCount + index);

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
                                AddOutputIndex(input.ConnectedOutput);
                            }
                            break;

                        case UpdateParameterType.Output:
                            var output = node.GetOutput(parameter.Index);
                            AddOutputIndex(output);
                            break;

                        case UpdateParameterType.State:
                            AddStateIndex(node, parameter.Index);
                            break;
                    }
                }
            }

            return valueIndices;
        }

        private NextDelegate GenerateNextMethod(Node[] nodes, Dictionary<(Node Node, int Index), int> indices)
        {
            var paramTypes = new[]
            {
                typeof(float[]),
                typeof(SpecialParameters)
            };

            var method = new DynamicMethod("Next", typeof(void), paramTypes, typeof(Simulation).Module);

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

                        case UpdateParameterType.Special:
                            ilGen.Emit(OpCodes.Ldarga_S, (byte) 1);
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

            return method.CreateDelegate<NextDelegate>();
        }
    }
}
