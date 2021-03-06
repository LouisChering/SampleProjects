using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using FunctionApp2;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SlackConnector.Models;

namespace SlackMessageTriggerExtention
{
    internal class SlackMessageTriggerBinding : ITriggerBinding
    {
        private readonly Dictionary<string, Type> _bindingContract;
        private readonly string _functionName;
        private readonly ParameterInfo _parameter;
        private SlackMessageExtentionConfig _listenersStore;

        public SlackMessageTriggerBinding(ParameterInfo parameter, SlackMessageExtentionConfig listenersStore,
            string functionName)
        {
            _parameter = parameter;
            _listenersStore = listenersStore;
            _functionName = functionName;
            _bindingContract = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                {"data", typeof(JObject)}
            };
        }

        public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            if (value is SlackMessage)
            {
                var bindingData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    {"data", value}
                };

                object argument;
                if (_parameter.ParameterType == typeof(string))
                    argument = JsonConvert.SerializeObject(value, Formatting.Indented);
                else
                    argument = value;

                IValueBinder valueBinder = new SlackMessageValueBinder(_parameter, argument);
                return Task.FromResult<ITriggerData>(new TriggerData(valueBinder, bindingData));
            }
            throw new Exception();
        }

        public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
        {
            var attribute = GetResolvedAttribute<SlackMessageTriggerAttribute>(_parameter);
            return Task.FromResult<IListener>(new SlackMessageListener(context.Executor, attribute));
        }

        /// <summary>Get a description of the binding.</summary>
        /// <returns>The <see cref="T:Microsoft.Azure.WebJobs.Host.Protocols.ParameterDescriptor" /></returns>
        public ParameterDescriptor ToParameterDescriptor()
        {
            return new SlackMessageTriggerParameterDescriptor
            {
                Name = _parameter.Name,
                DisplayHints = new ParameterDisplayHints
                {
                    Prompt = "SlackMessage",
                    Description = "SlackMessage trigger fired",
                    DefaultValue = "Sample"
                }
            };
        }

        public Type TriggerValueType => typeof(SlackMessage);

        /// <summary>Gets the binding data contract.</summary>
        public IReadOnlyDictionary<string, Type> BindingDataContract => _bindingContract;

        internal static TAttribute GetResolvedAttribute<TAttribute>(ParameterInfo parameter)
            where TAttribute : Attribute
        {
            var attribute = parameter.GetCustomAttribute<TAttribute>(true);

            var attributeConnectionProvider = attribute as IConnectionProvider;
            if (attributeConnectionProvider != null && string.IsNullOrEmpty(attributeConnectionProvider.Connection))
            {
                var connectionProviderAttribute =
                    attribute.GetType().GetCustomAttribute<ConnectionProviderAttribute>();
                if (connectionProviderAttribute?.ProviderType != null)
                {
                    var connectionOverrideProvider =
                        GetHierarchicalAttributeOrNull(parameter, connectionProviderAttribute.ProviderType) as
                            IConnectionProvider;
                    if (connectionOverrideProvider != null &&
                        !string.IsNullOrEmpty(connectionOverrideProvider.Connection))
                        attributeConnectionProvider.Connection = connectionOverrideProvider.Connection;
                }
            }

            return attribute;
        }

        internal static T GetHierarchicalAttributeOrNull<T>(ParameterInfo parameter) where T : Attribute
        {
            return (T)GetHierarchicalAttributeOrNull(parameter, typeof(T));
        }

        internal static Attribute GetHierarchicalAttributeOrNull(ParameterInfo parameter, Type attributeType)
        {
            if (parameter == null)
                return null;

            var attribute = parameter.GetCustomAttribute(attributeType);
            if (attribute != null)
                return attribute;

            var method = parameter.Member as MethodInfo;
            if (method == null)
                return null;
            return GetHierarchicalAttributeOrNull(method, attributeType);
        }

        internal static T GetHierarchicalAttributeOrNull<T>(MethodInfo method) where T : Attribute
        {
            return (T)GetHierarchicalAttributeOrNull(method, typeof(T));
        }

        internal static Attribute GetHierarchicalAttributeOrNull(MethodInfo method, Type type)
        {
            var attribute = method.GetCustomAttribute(type);
            if (attribute != null)
                return attribute;

            attribute = method.DeclaringType.GetCustomAttribute(type);
            if (attribute != null)
                return attribute;

            return null;
        }

        private class SlackMessageValueBinder : ValueBinder, IDisposable
        {
            private readonly object _value;
            private List<IDisposable> _disposables;

            public SlackMessageValueBinder(ParameterInfo parameter, object value,
                List<IDisposable> disposables = null)
                : base(parameter.ParameterType)
            {
                _value = value;
                _disposables = disposables;
            }

            public void Dispose()
            {
                if (_disposables != null)
                {
                    foreach (var d in _disposables)
                        d.Dispose();
                    _disposables = null;
                }
            }

            public override Task<object> GetValueAsync()
            {
                return Task.FromResult(_value);
            }

            public override string ToInvokeString()
            {
                // TODO: Customize your Dashboard invoke string
                return $"{_value}";
            }
        }

        private class SlackMessageTriggerParameterDescriptor : TriggerParameterDescriptor
        {
            public override string GetTriggerReason(IDictionary<string, string> arguments)
            {
                // TODO: Customize your Dashboard display string
                return string.Format("SlackMessage trigger fired at {0}", DateTime.Now.ToString("o"));
            }
        }
    }
}