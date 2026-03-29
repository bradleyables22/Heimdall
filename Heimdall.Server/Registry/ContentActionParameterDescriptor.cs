using Heimdall.Server.Registry;
using System.Reflection;

namespace Heimdall.Server
{
    internal sealed class ContentActionParameterDescriptor
    {
        public int Index { get; }

        public ParameterInfo Parameter { get; }

        public Type ParameterType { get; }

        public ContentActionParameterKind Kind { get; }

        public ContentActionParameterDescriptor(
            int index,
            ParameterInfo parameter,
            Type parameterType,
            ContentActionParameterKind kind)
        {
            Index = index;
            Parameter = parameter;
            ParameterType = parameterType;
            Kind = kind;
        }
    }
}