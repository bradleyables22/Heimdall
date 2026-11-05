using Heimdall.Server.Registry;
using Microsoft.AspNetCore.Http.Metadata;
using System.Reflection;

namespace Heimdall.Server
{
    internal sealed class ContentInvocationParameterDescriptor
    {
        public int Index { get; }

        public ParameterInfo Parameter { get; }

        public Type ParameterType { get; }

        public ContentInvocationParameterKind Kind { get; }

        public string BindingName { get; }

        public ContentInvocationParameterDescriptor(
            int index,
            ParameterInfo parameter,
            Type parameterType,
            ContentInvocationParameterKind kind)
        {
            Index = index;
            Parameter = parameter;
            ParameterType = parameterType;
            Kind = kind;
            BindingName = parameter
                .GetCustomAttributes(inherit: true)
                .OfType<IFromFormMetadata>()
                .Select(metadata => metadata.Name)
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
                ?? parameter.Name
                ?? $"arg{index}";
        }
    }
}
