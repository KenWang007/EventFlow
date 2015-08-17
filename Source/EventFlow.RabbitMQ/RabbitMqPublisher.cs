// The MIT License (MIT)
//
// Copyright (c) 2015 Rasmus Mikkelsen
// https://github.com/rasmus/EventFlow
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventFlow.Aggregates;
using EventFlow.Core;
using EventFlow.Extensions;
using RabbitMQ.Client;

namespace EventFlow.RabbitMQ
{
    public class RabbitMqPublisher : IRabbitMqPublisher
    {
        private readonly IRabbitMqConnectionFactory _connectionFactory;
        private readonly IRabbitMqMessageFactory _messageFactory;
        private readonly IRabbitMqConfiguration _configuration;
        private readonly ITransientFaultHandler<IRabbitMqRetryStrategy> _transientFaultHandler;

        public RabbitMqPublisher(
            IRabbitMqConnectionFactory connectionFactory,
            IRabbitMqMessageFactory messageFactory,
            IRabbitMqConfiguration configuration,
            ITransientFaultHandler<IRabbitMqRetryStrategy> transientFaultHandler)
        {
            _connectionFactory = connectionFactory;
            _messageFactory = messageFactory;
            _configuration = configuration;
            _transientFaultHandler = transientFaultHandler;
        }

        public async Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
        {
            var message = await _messageFactory.CreateMessageAsync(domainEvent, cancellationToken).ConfigureAwait(false);

            await _transientFaultHandler.TryAsync(
                c => PublishAsync(message, c),
                Label.Named("rabbitmq-publish"),
                cancellationToken)
                .ConfigureAwait(false);
        }

        private async Task<int> PublishAsync(RabbitMqMessage message, CancellationToken cancellationToken)
        {
            // TODO: Cache connection/model

            using (var connection = await _connectionFactory.CreateConnectionAsync(_configuration.Uri, cancellationToken).ConfigureAwait(false))
            using (var model = connection.CreateModel())
            {
                var basicProperties = model.CreateBasicProperties();
                basicProperties.Headers = message.Headers.ToDictionary(kv => kv.Value, kv => (object)kv.Value);
                basicProperties.Persistent = true; // TODO: get from config
                basicProperties.Timestamp = new AmqpTimestamp(DateTimeOffset.Now.ToUnixTime());

                model.BasicPublish("get from config", "get from config", false, basicProperties, message.Message);
            }

            return 0;
        }
    }
}

