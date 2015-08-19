﻿// The MIT License (MIT)
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
using EventFlow.Aggregates;
using EventFlow.EventStores;
using EventFlow.Extensions;
using EventFlow.RabbitMQ.Extensions;
using EventFlow.TestHelpers;
using EventFlow.TestHelpers.Aggregates.Test;
using EventFlow.TestHelpers.Aggregates.Test.Commands;
using EventFlow.TestHelpers.Aggregates.Test.Events;
using EventFlow.TestHelpers.Aggregates.Test.ValueObjects;
using FluentAssertions;
using NUnit.Framework;

namespace EventFlow.RabbitMQ.Tests.Integration
{
    public class RabbitMqTests
    {
        public class RabbitMqCommittedEvent : ICommittedDomainEvent
        {
            public string AggregateId { get; set; }
            public string Data { get; set; }
            public string Metadata { get; set; }
            public int AggregateSequenceNumber { get; set; }
        }

        [Test, Explicit("Needs RabbitMQ running localhost (https://github.com/rasmus/Vagrant.Boxes)")]
        public void Test()
        {
            var uri = new Uri("amqp://localhost");
            using (var consumer = new RabbitMqConsumer(uri, "eventflow", new[] {"#"}))
            {
                var resolver = EventFlowOptions.New
                    .PublishToRabbitMq(RabbitMqConfiguration.With(uri))
                    .AddDefaults(EventFlowTestHelpers.Assembly)
                    .CreateResolver(false);

                var commandBus = resolver.Resolve<ICommandBus>();
                var eventJsonSerializer = resolver.Resolve<IEventJsonSerializer>();

                var pingId = PingId.New;
                commandBus.Publish(new PingCommand(TestId.New, pingId), CancellationToken.None);

                var rabbitMqMessage = consumer.GetMessages().Single();
                rabbitMqMessage.Exchange.Value.Should().Be("eventflow");
                rabbitMqMessage.RoutingKey.Value.Should().Be("eventflow.domainevent.test.ping-event.1");
                
                var pingEvent = (IDomainEvent<TestAggregate, TestId, PingEvent>) eventJsonSerializer.Deserialize(
                    rabbitMqMessage.Message,
                    new Metadata(rabbitMqMessage.Headers));

                pingEvent.AggregateEvent.PingId.Should().Be(pingId);
            }
        }
    }
}
