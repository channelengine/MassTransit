namespace MassTransit.RabbitMqTransport
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using Topology;
    using Util;


    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + "}")]
    public readonly struct RabbitMqEndpointAddress
    {
        const string AutoDeleteKey = "autodelete";
        const string DurableKey = "durable";
        const string TemporaryKey = "temporary";
        const string ExchangeTypeKey = "type";
        const string BindQueueKey = "bind";
        const string QueueNameKey = "queue";
        const string AlternateExchangeKey = "alternateexchange";
        const string BindExchangeKey = "bindexchange";
        const string DelayedTypeKey = "delayedtype";

        const string ExchangeArgumentsKeyPrefix = "args-";
        const string QueueArgumentsKeyPrefix = "queueargs-";

        const string DelayedMessageExchangeType = "x-delayed-message";

        public readonly string Scheme;
        public readonly string Host;
        public readonly int? Port;
        public readonly string VirtualHost;

        public readonly string Name;
        public readonly string ExchangeType;
        public readonly bool Durable;
        public readonly bool AutoDelete;
        public readonly bool BindToQueue;
        public readonly string QueueName;
        public readonly string DelayedType;
        public readonly string[] BindExchanges;
        public readonly string AlternateExchange;

        public readonly Dictionary<string, string> ExchangeArguments;
        public readonly Dictionary<string, string> QueueArguments;

        public RabbitMqEndpointAddress(Uri hostAddress, Uri address)
        {
            Scheme = default;
            Host = default;
            Port = default;
            VirtualHost = default;

            Durable = true;
            AutoDelete = false;
            ExchangeType = RabbitMQ.Client.ExchangeType.Fanout;

            BindToQueue = false;
            QueueName = default;
            DelayedType = default;
            AlternateExchange = default;
            BindExchanges = default;

            ExchangeArguments = new Dictionary<string, string>();
            QueueArguments = new Dictionary<string, string>();

            var scheme = address.Scheme.ToLowerInvariant();
            if (scheme.EndsWith("s"))
                Port = 5671;

            switch (scheme)
            {
                case "rabbitmqs":
                case "amqps":
                case "rabbitmq":
                case "amqp":
                    Scheme = address.Scheme;
                    Host = address.Host;
                    Port = !address.IsDefaultPort ? address.Port : scheme.EndsWith("s") ? 5671 : 5672;

                    address.ParseHostPathAndEntityName(out VirtualHost, out Name);
                    break;

                case "queue":
                    ParseLeft(hostAddress, out Scheme, out Host, out Port, out VirtualHost);

                    Name = address.AbsolutePath;
                    BindToQueue = true;
                    break;

                case "exchange":
                    ParseLeft(hostAddress, out Scheme, out Host, out Port, out VirtualHost);

                    Name = address.AbsolutePath;
                    break;

                default:
                    throw new ArgumentException($"The address scheme is not supported: {address.Scheme}", nameof(address));
            }

            if (Name == "*")
                Name = NewId.Next().ToString("NS");

            RabbitMqEntityNameValidator.Validator.ThrowIfInvalidEntityName(Name);

            var bindExchanges = new HashSet<string>();

            foreach (var (key, value) in address.SplitQueryString())
            {
                if (ParseArguments(key, value, ExchangeArgumentsKeyPrefix, ExchangeArguments))
                    continue;

                if (ParseArguments(key, value, QueueArgumentsKeyPrefix, QueueArguments))
                    continue;

                switch (key)
                {
                    case TemporaryKey when bool.TryParse(value, out var result):
                        AutoDelete = result;
                        Durable = !result;
                        break;

                    case DurableKey when bool.TryParse(value, out var result):
                        Durable = result;
                        break;

                    case AutoDeleteKey when bool.TryParse(value, out var result):
                        AutoDelete = result;
                        break;

                    case ExchangeTypeKey when !string.IsNullOrWhiteSpace(value):
                        ExchangeType = value;
                        break;

                    case BindQueueKey when bool.TryParse(value, out var result):
                        BindToQueue = result;
                        break;

                    case QueueNameKey when !string.IsNullOrWhiteSpace(value):
                        QueueName = Uri.UnescapeDataString(value);
                        break;

                    case DelayedTypeKey when !string.IsNullOrWhiteSpace(value):
                        DelayedType = value;
                        ExchangeType = DelayedMessageExchangeType;
                        break;

                    case AlternateExchangeKey when !string.IsNullOrWhiteSpace(value):
                        AlternateExchange = value;
                        break;

                    case BindExchangeKey when !string.IsNullOrWhiteSpace(value):
                        bindExchanges.Add(value);
                        break;
                }
            }

            if (bindExchanges.Count > 0)
                BindExchanges = bindExchanges.ToArray();
        }

        public RabbitMqEndpointAddress(Uri hostAddress, string exchangeName, string exchangeType = default, bool durable = true, bool autoDelete = false,
            bool bindToQueue = false, string queueName = default, string delayedType = default, string[] bindExchanges = default,
            string alternateExchange = default, Dictionary<string, string> exchangeArguments = default, Dictionary<string, string> queueArguments = default)
        {
            ParseLeft(hostAddress, out Scheme, out Host, out Port, out VirtualHost);

            Name = exchangeName;
            ExchangeType = exchangeType ?? RabbitMQ.Client.ExchangeType.Fanout;

            Durable = durable;
            AutoDelete = autoDelete;
            BindToQueue = bindToQueue;
            QueueName = queueName;
            DelayedType = delayedType;
            BindExchanges = bindExchanges;
            AlternateExchange = alternateExchange;

            ExchangeArguments = exchangeArguments;
            QueueArguments = queueArguments;
        }

        RabbitMqEndpointAddress(string scheme, string host, int? port, string virtualHost, string name, string exchangeType, bool durable,
            bool autoDelete, bool bindToQueue, string queueName, string delayedType, string[] bindExchanges, string alternateExchange,
            Dictionary<string, string> exchangeArguments = default, Dictionary<string, string> queueArguments = default)
        {
            Scheme = scheme;
            Host = host;
            Port = port;
            VirtualHost = virtualHost;
            Name = name;
            ExchangeType = exchangeType;
            Durable = durable;
            AutoDelete = autoDelete;
            BindToQueue = bindToQueue;
            QueueName = queueName;
            DelayedType = delayedType;
            BindExchanges = bindExchanges;
            AlternateExchange = alternateExchange;

            ExchangeArguments = exchangeArguments;
            QueueArguments = queueArguments;
        }

        public RabbitMqEndpointAddress GetDelayAddress()
        {
            var name = $"{Name}_delay";

            return new RabbitMqEndpointAddress(Scheme, Host, Port, VirtualHost, name, DelayedMessageExchangeType, Durable, AutoDelete, false,
                default, ExchangeType, BindExchanges, AlternateExchange);
        }

        static void ParseLeft(Uri address, out string scheme, out string host, out int? port, out string virtualHost)
        {
            var hostAddress = new RabbitMqHostAddress(address);
            scheme = hostAddress.Scheme;
            host = hostAddress.Host;
            port = hostAddress.Port;
            virtualHost = hostAddress.VirtualHost;
        }

        static bool ParseArguments(string key, string value, string prefix, Dictionary<string, string> arguments)
        {
            if (string.IsNullOrEmpty(key)
                || !key.StartsWith(prefix)
                || string.IsNullOrEmpty(prefix)
                || string.IsNullOrEmpty(value)
                || arguments == null
            ) return false;

            var argumentKey = key.Replace(prefix, string.Empty);
            if (string.IsNullOrEmpty(argumentKey))
                return false;

            arguments[argumentKey] = value;
            return true;

        }

        public static implicit operator Uri(in RabbitMqEndpointAddress address)
        {
            var builder = new UriBuilder
            {
                Scheme = address.Scheme,
                Host = address.Host,
                Port = address.Port.HasValue
                    ? address.Scheme.EndsWith("s", StringComparison.OrdinalIgnoreCase)
                        ? address.Port.Value == 5671 ? 0 : address.Port.Value
                        : address.Port.Value == 5672
                            ? 0
                            : address.Port.Value
                    : 0,
                Path = address.VirtualHost == "/"
                    ? $"/{address.Name}"
                    : $"/{Uri.EscapeDataString(address.VirtualHost)}/{address.Name}"
            };

            builder.Query += string.Join("&", address.GetQueryStringOptions());

            return builder.Uri;
        }

        Uri DebuggerDisplay => this;

        IEnumerable<string> GetQueryStringOptions()
        {
            if (!Durable && AutoDelete)
                yield return $"{TemporaryKey}=true";
            else if (!Durable)
                yield return $"{DurableKey}=false";
            else if (AutoDelete)
                yield return $"{AutoDeleteKey}=true";

            var noDelayedType = string.IsNullOrWhiteSpace(DelayedType);

            if (ExchangeType != RabbitMQ.Client.ExchangeType.Fanout && noDelayedType)
                yield return $"{ExchangeTypeKey}={ExchangeType}";

            if (BindToQueue)
                yield return $"{BindQueueKey}=true";
            if (!string.IsNullOrWhiteSpace(QueueName))
                yield return $"{QueueNameKey}={Uri.EscapeDataString(QueueName)}";

            if (!noDelayedType)
                yield return $"{DelayedTypeKey}={DelayedType}";

            if (!string.IsNullOrWhiteSpace(AlternateExchange))
                yield return $"{AlternateExchangeKey}={AlternateExchange}";

            if (BindExchanges != null)
            {
                foreach (var binding in BindExchanges)
                    yield return $"{BindExchangeKey}={binding}";
            }
        }
    }
}
