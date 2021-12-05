using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Wobbler
{
    public enum UpdateParameterType
    {
        Input,
        Output,
        State,
        Global
    }

    public readonly struct GlobalParameters
    {
        private static Dictionary<string, PropertyInfo> Properties { get; } = typeof(GlobalParameters)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);

        public static bool TryGetParameter(ParameterInfo paramInfo, out PropertyInfo propertyInfo)
        {
            propertyInfo = default;

            return paramInfo.Name != null && !paramInfo.ParameterType.IsByRef
                && Properties.TryGetValue(paramInfo.Name, out propertyInfo)
                && propertyInfo.PropertyType == paramInfo.ParameterType;
        }

        public float DeltaTime { get; }

        public GlobalParameters(float deltaTime)
        {
            DeltaTime = deltaTime;
        }
    }

    public record UpdateMethodParameter(
        ParameterInfo Parameter,
        PropertyInfo Property,
        UpdateParameterType Type,
        int Index);

    public class NodeType
    {
        private static Dictionary<Type, NodeType> Cache { get; } = new();

        public static NodeType Get(Node node)
        {
            var type = node.GetType();

            if (Cache.TryGetValue(type, out var cached))
            {
                return cached;
            }

            cached = new NodeType(type);
            Cache.Add(type, cached);

            return cached;
        }

        private static bool IsValidParameterType(Type type)
        {
            if (type.IsByRef)
            {
                type = type.GetElementType();
            }

            if (!type!.IsValueType) return false;

            if (type == typeof(Input)) return true;
            if (type == typeof(Output)) return true;
            if (type == typeof(float)) return true;
            if (type == typeof(int)) return true;
            if (type.IsEnum) return true;

            return false;
        }

        public Type Type { get; }
        public MethodInfo UpdateMethod { get; }
        public UpdateMethodParameter[] UpdateMethodParameters { get; }

        public int InputCount => InputProperties.Length;
        public int OutputCount => OutputProperties.Length;
        public int StateCount => StateProperties.Length;

        public int ValueCount => OutputCount + StateCount;

        public PropertyInfo[] InputProperties { get; }
        public PropertyInfo[] OutputProperties { get; }
        public PropertyInfo[] StateProperties { get; }

        private readonly Dictionary<string, PropertyInfo> _properties;

        private NodeType(Type type)
        {
            Type = type;

            UpdateMethod = type
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(x => x.GetCustomAttribute<NextMethodAttribute>() != null);

            if (UpdateMethod == null)
            {
                throw new Exception($"Type must have a public static method marked with {nameof(NextMethodAttribute)}.");
            }

            _properties = Type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(x => x.GetGetMethod(true) != null && (x.PropertyType != typeof(Input) && x.PropertyType != typeof(Output) || x.GetGetMethod(true) != null) && IsValidParameterType(x.PropertyType))
                .ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);

            var parameters = UpdateMethod.GetParameters();

            var inputCount = 0;
            var outputCount = 0;
            var stateCount = 0;

            UpdateMethodParameters = new UpdateMethodParameter[parameters.Length];

            for (var i = 0; i < parameters.Length; ++i)
            {
                UpdateMethodParameters[i] = GetUpdateMethodParameter(parameters[i],
                    ref inputCount, ref outputCount, ref stateCount);
            }

            InputProperties = UpdateMethodParameters
                .Where(x => x.Type == UpdateParameterType.Input)
                .Select(x => x.Property)
                .ToArray();

            OutputProperties = UpdateMethodParameters
                .Where(x => x.Type == UpdateParameterType.Output)
                .Select(x => x.Property)
                .ToArray();

            StateProperties = UpdateMethodParameters
                .Where(x => x.Type == UpdateParameterType.State)
                .Select(x => x.Property)
                .ToArray();

            Debug.Assert(inputCount == InputCount);
            Debug.Assert(outputCount == OutputCount);
            Debug.Assert(stateCount == StateCount);
        }

        private bool TryGetProperty(ParameterInfo paramInfo, out PropertyInfo propInfo, out UpdateParameterType type)
        {
            propInfo = default;
            type = default;

            if (paramInfo.Name == null) return false;

            if (!_properties.TryGetValue(paramInfo.Name, out propInfo))
            {
                return false;
            }

            if (propInfo.PropertyType == typeof(Input))
            {
                type = UpdateParameterType.Input;
                return paramInfo.ParameterType == typeof(float) && !paramInfo.ParameterType.IsByRef && !paramInfo.IsOut;
            }

            if (propInfo.PropertyType == typeof(Output))
            {
                type = UpdateParameterType.Output;
                return paramInfo.ParameterType.IsByRef && paramInfo.ParameterType.GetElementType() == typeof(float);
            }

            type = UpdateParameterType.State;

            if (paramInfo.IsOut) return false;

            return paramInfo.ParameterType.IsByRef
                ? paramInfo.ParameterType.GetElementType() == propInfo.PropertyType
                : paramInfo.ParameterType == propInfo.PropertyType;
        }

        private UpdateMethodParameter GetUpdateMethodParameter(ParameterInfo paramInfo,
            ref int inputCount, ref int outputCount, ref int stateCount)
        {
            if (!IsValidParameterType(paramInfo.ParameterType))
            {
                throw new Exception(
                    $"Invalid update method parameter type.{Environment.NewLine}" +
                    $"Method: {UpdateMethod.DeclaringType}::{UpdateMethod.Name}{Environment.NewLine}" +
                    $"Parameter: {paramInfo.Name}");
            }

            if (GlobalParameters.TryGetParameter(paramInfo, out var propInfo))
            {
                return new UpdateMethodParameter(paramInfo, propInfo, UpdateParameterType.Global, 0);
            }

            if (!TryGetProperty(paramInfo, out propInfo, out var type))
            {
                throw new Exception(
                    $"Unable to find a matching property for update method parameter.{Environment.NewLine}" +
                    $"Method: {UpdateMethod.DeclaringType}::{UpdateMethod.Name}{Environment.NewLine}" +
                    $"Parameter: {paramInfo.Name}");
            }

            var index = type switch
            {
                UpdateParameterType.Input => inputCount++,
                UpdateParameterType.Output => outputCount++,
                UpdateParameterType.State => stateCount++,
                _ => throw new Exception()
            };

            return new UpdateMethodParameter(paramInfo, propInfo, type, index);
        }
    }
}
