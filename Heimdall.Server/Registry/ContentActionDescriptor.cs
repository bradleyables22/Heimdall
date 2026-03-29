using Heimdall.Server.Registry;
using Microsoft.AspNetCore.Html;
using System.Linq.Expressions;
using System.Reflection;

namespace Heimdall.Server
{
    internal sealed class ContentActionDescriptor
    {
        private readonly Func<object?[], object?> _invoker;

        public string ActionId { get; }

        public MethodInfo Method { get; }

        public IReadOnlyList<ContentActionParameterDescriptor> Parameters { get; }

        public ContentActionReturnKind ReturnKind { get; }

        public bool HasPayload => PayloadParameter is not null;

        public ContentActionParameterDescriptor? PayloadParameter { get; }

        public Type? PayloadType => PayloadParameter?.ParameterType;

        public ContentActionDescriptor(
            string actionId,
            MethodInfo method,
            IReadOnlyList<ContentActionParameterDescriptor> parameters,
            ContentActionReturnKind returnKind)
        {
            ActionId = actionId;
            Method = method;
            Parameters = parameters;
            ReturnKind = returnKind;
            PayloadParameter = parameters.FirstOrDefault(x => x.Kind == ContentActionParameterKind.Payload);
            _invoker = CompileInvoker(method);
        }

        public async ValueTask<IHtmlContent?> InvokeAsync(object?[] args)
        {
            var result = _invoker(args);

            if (result is null)
                return null;

            return ReturnKind switch
            {
                ContentActionReturnKind.Html => (IHtmlContent)result,
                ContentActionReturnKind.TaskHtml => await (Task<IHtmlContent>)result,
                ContentActionReturnKind.ValueTaskHtml => await (ValueTask<IHtmlContent>)result,
                _ => throw new InvalidOperationException(
                    $"Unsupported Heimdall return kind '{ReturnKind}' for '{Method.DeclaringType?.FullName}.{Method.Name}'.")
            };
        }

        private static Func<object?[], object?> CompileInvoker(MethodInfo method)
        {
            var argsParam = Expression.Parameter(typeof(object?[]), "args");

            var methodParams = method.GetParameters();
            var callArgs = new Expression[methodParams.Length];

            for (int i = 0; i < methodParams.Length; i++)
            {
                var indexExpr = Expression.ArrayIndex(argsParam, Expression.Constant(i));
                callArgs[i] = Expression.Convert(indexExpr, methodParams[i].ParameterType);
            }

            var callExpr = Expression.Call(method, callArgs);

            Expression body;

            if (method.ReturnType == typeof(void))
            {
                body = Expression.Block(callExpr, Expression.Constant(null, typeof(object)));
            }
            else if (method.ReturnType.IsValueType)
            {
                body = Expression.Convert(callExpr, typeof(object));
            }
            else
            {
                body = Expression.TypeAs(callExpr, typeof(object));
            }

            return Expression.Lambda<Func<object?[], object?>>(body, argsParam).Compile();
        }
    }
}