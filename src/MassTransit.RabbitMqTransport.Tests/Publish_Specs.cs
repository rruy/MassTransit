﻿// Copyright 2007-2016 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit.RabbitMqTransport.Tests
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Shouldly;

    namespace Send_Specs
    {
        using System;
        using System.Linq;
        using System.Threading.Tasks;
        using MassTransit.Testing;
        using NUnit.Framework;
        using Serialization;
        using Shouldly;
        using TestFramework;


        [TestFixture]
        public class WhenAMessageIsSendToTheEndpoint :
            RabbitMqTestFixture
        {
            [Test]
            public async Task Should_be_received()
            {
                var endpoint = await Bus.GetSendEndpoint(InputQueueAddress);

                var message = new A {Id = Guid.NewGuid()};
                await endpoint.Send(message);

                ConsumeContext<A> received = await _receivedA;

                Assert.AreEqual(message.Id, received.Message.Id);
            }

            Task<ConsumeContext<A>> _receivedA;

            protected override void ConfigureInputQueueEndpoint(IRabbitMqReceiveEndpointConfigurator configurator)
            {
                base.ConfigureInputQueueEndpoint(configurator);

                _receivedA = Handled<A>(configurator);
            }
        }


        [TestFixture]
        public class When_a_message_is_send_to_the_bus_itself :
            RabbitMqTestFixture
        {
            [Test]
            public async Task Should_be_received()
            {
                Task<ConsumeContext<A>> receivedA = SubscribeHandler<A>();

                var message = new A {Id = Guid.NewGuid()};
                await BusSendEndpoint.Send(message);

                ConsumeContext<A> received = await receivedA;

                Assert.AreEqual(message.Id, received.Message.Id);

            }
        }


        [TestFixture]
        public class WhenAMessageIsSendToTheEndpointEncrypted :
            RabbitMqTestFixture
        {
            [Test]
            public async Task Should_be_received()
            {
                var endpoint = await Bus.GetSendEndpoint(InputQueueAddress);

                var message = new A {Id = Guid.NewGuid()};
                await endpoint.Send(message);

                ConsumeContext<A> received = await _receivedA;

                Assert.AreEqual(message.Id, received.Message.Id);

                Assert.AreEqual(EncryptedMessageSerializer.EncryptedContentType, received.ReceiveContext.ContentType);
            }

            Task<ConsumeContext<A>> _receivedA;

            protected override void ConfigureInputQueueEndpoint(IRabbitMqReceiveEndpointConfigurator configurator)
            {
                base.ConfigureInputQueueEndpoint(configurator);

                _receivedA = Handled<A>(configurator);
            }

            protected override void ConfigureBus(IRabbitMqBusFactoryConfigurator configurator)
            {
                ISymmetricKeyProvider keyProvider = new TestSymmetricKeyProvider();
                var streamProvider = new AesCryptoStreamProvider(keyProvider, "default");
                configurator.UseEncryptedSerializer(streamProvider);

                base.ConfigureBus(configurator);
            }
        }


        [TestFixture]
        public class WhenAMessageIsPublishedToTheEndpoint :
            RabbitMqTestFixture
        {
            [Test]
            public async Task Should_be_received()
            {
                var message = new A {Id = Guid.NewGuid()};
                await Bus.Publish(message);

                ConsumeContext<A> received = await _receivedA;

                Assert.AreEqual(message.Id, received.Message.Id);
            }

            Task<ConsumeContext<A>> _receivedA;

            protected override void ConfigureInputQueueEndpoint(IRabbitMqReceiveEndpointConfigurator configurator)
            {
                base.ConfigureInputQueueEndpoint(configurator);

                _receivedA = Handled<A>(configurator);
            }
        }


        [TestFixture]
        public class WhenAMessageIsPublishedToATemporaryEndpoint :
            RabbitMqTestFixture
        {
            [Test]
            public async Task Should_be_received()
            {
                var message = new A {Id = Guid.NewGuid()};

                await Bus.Publish(message);

                await _receivedA;
                await _temporaryA;
                await _temporaryB;
            }

            Task<ConsumeContext<A>> _receivedA;
            Task<ConsumeContext<A>> _temporaryA;
            Task<ConsumeContext<A>> _temporaryB;

            protected override void ConfigureBusHost(IRabbitMqBusFactoryConfigurator configurator, IRabbitMqHost host)
            {
                base.ConfigureBusHost(configurator, host);

                configurator.ReceiveEndpoint(host, x =>
                {
                    _temporaryA = Handled<A>(x);
                });

                configurator.ReceiveEndpoint(host, x =>
                {
                    _temporaryB = Handled<A>(x);
                });
            }

            protected override void ConfigureInputQueueEndpoint(IRabbitMqReceiveEndpointConfigurator configurator)
            {
                base.ConfigureInputQueueEndpoint(configurator);

                _receivedA = Handled<A>(configurator);
            }
        }


        [TestFixture]
        public class When_a_message_is_published_from_the_queue :
            RabbitMqTestFixture
        {
            [Test]
            public async Task Should_have_the_receive_endpoint_input_address()
            {
                var message = new A {Id = Guid.NewGuid()};
                await Bus.Publish(message);

                ConsumeContext<A> received = await _receivedA;

                Assert.AreEqual(message.Id, received.Message.Id);

                ConsumeContext<GotA> consumeContext = await _receivedGotA;

                consumeContext.SourceAddress.ShouldBe(new Uri("rabbitmq://[::1]/test/input_queue"));
            }

            Task<ConsumeContext<A>> _receivedA;
            Task<ConsumeContext<GotA>> _receivedGotA;

            protected override void ConfigureInputQueueEndpoint(IRabbitMqReceiveEndpointConfigurator configurator)
            {
                base.ConfigureInputQueueEndpoint(configurator);

                configurator.PrefetchCount = 16;

                _receivedA = Handler<A>(configurator, context => context.Publish(new GotA {Id = context.Message.Id}));
            }

            protected override void ConfigureBusHost(IRabbitMqBusFactoryConfigurator configurator, IRabbitMqHost host)
            {
                base.ConfigureBusHost(configurator, host);

                configurator.ReceiveEndpoint(host, "ack_queue", x =>
                {
                    _receivedGotA = Handled<GotA>(x);
                });
            }
        }


        [TestFixture]
        public class WhenAMessageIsPublishedToTheConsumer :
            RabbitMqTestFixture
        {
            [Test]
            public async Task Should_be_received()
            {
                var message = new B {Id = Guid.NewGuid()};

                await Bus.Publish(message);

                _consumer.Received.Select<B>().Any().ShouldBe(true);

                IReceivedMessage<B> receivedMessage = _consumer.Received.Select<B>().First();

                Assert.AreEqual(message.Id, receivedMessage.Context.Message.Id);
            }

            MultiTestConsumer _consumer;

            protected override void ConfigureInputQueueEndpoint(IRabbitMqReceiveEndpointConfigurator configurator)
            {
                base.ConfigureInputQueueEndpoint(configurator);

                _consumer = new MultiTestConsumer(TestTimeout);
                _consumer.Consume<B>();

                _consumer.Configure(configurator);
            }
        }


        [TestFixture]
        public class When_a_message_is_published_without_a_queue_binding :
            RabbitMqTestFixture
        {
            [Test]
            public async Task Should_not_throw_an_exception()
            {
                var message = new UnboundMessage {Id = Guid.NewGuid()};

                await Bus.Publish(message);
            }


            class UnboundMessage
            {
                public Guid Id { get; set; }
            }
        }


        [TestFixture]
        public class When_a_message_is_sent_with_no_subscriber :
            RabbitMqTestFixture
        {
            [Test]
            public async Task Should_not_throw_an_exception()
            {
                var message = new UnboundMessage {Id = Guid.NewGuid()};

                await InputQueueSendEndpoint.Send(message);

                await InputQueueSendEndpoint.Send(new B());

                _consumer.Received.Select<B>().Any().ShouldBe(true);
            }

            MultiTestConsumer _consumer;

            protected override void ConfigureInputQueueEndpoint(IRabbitMqReceiveEndpointConfigurator configurator)
            {
                base.ConfigureInputQueueEndpoint(configurator);

                configurator.UseConcurrencyLimit(1);

                _consumer = new MultiTestConsumer(TestTimeout);
                _consumer.Consume<B>();

                _consumer.Configure(configurator);
            }


            class UnboundMessage
            {
                public Guid Id { get; set; }
            }
        }


        class A
        {
            public Guid Id { get; set; }
        }


        class GotA
        {
            public Guid Id { get; set; }
        }


        class B : IEquatable<B>
        {
            public Guid Id { get; set; }

            public bool Equals(B other)
            {
                if (ReferenceEquals(null, other))
                    return false;
                if (ReferenceEquals(this, other))
                    return true;
                return Id.Equals(other.Id);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj))
                    return false;
                if (ReferenceEquals(this, obj))
                    return true;
                if (obj.GetType() != GetType())
                    return false;
                return Equals((B)obj);
            }

            public override int GetHashCode()
            {
                return Id.GetHashCode();
            }
        }
    }


    [TestFixture]
    public class When_publishing_an_interface_message :
        RabbitMqTestFixture
    {
        [Test]
        public async Task Should_have_correlation_id()
        {
            ConsumeContext<IProxyMe> message = await _handler;

            message.Message.CorrelationId.ShouldBe(_correlationId);
        }

        [Test]
        public async Task Should_have_integer_value()
        {
            ConsumeContext<IProxyMe> message = await _handler;

            message.Message.IntValue.ShouldBe(IntValue);
        }

        [Test]
        public async Task Should_have_received_message()
        {
            await _handler;
        }

        [Test]
        public async Task Should_have_string_value()
        {
            ConsumeContext<IProxyMe> message = await _handler;

            message.Message.StringValue.ShouldBe(StringValue);
        }

        const int IntValue = 42;
        const string StringValue = "Hello";
        readonly Guid _correlationId = Guid.NewGuid();
        Task<ConsumeContext<IProxyMe>> _handler;

        [OneTimeSetUp]
        public async Task Setup()
        {
            await InputQueueSendEndpoint.Send<IProxyMe>(new {IntValue, StringValue, CorrelationId = _correlationId});
        }

        protected override void ConfigureInputQueueEndpoint(IRabbitMqReceiveEndpointConfigurator configurator)
        {
            _handler = Handled<IProxyMe>(configurator);
        }


        public interface IProxyMe :
            CorrelatedBy<Guid>
        {
            int IntValue { get; }
            string StringValue { get; }
        }
    }
}