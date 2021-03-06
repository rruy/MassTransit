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
namespace MassTransit.MongoDbIntegration.Tests.Courier
{
    using System;
    using System.Threading.Tasks;
    using GreenPipes;
    using MassTransit.Courier.Contracts;
    using MongoDbIntegration.Courier;
    using MongoDbIntegration.Courier.Consumers;
    using MongoDbIntegration.Courier.Documents;
    using MongoDB.Driver;
    using NUnit.Framework;
    using Util;


    [TestFixture]
    public class When_a_complete_routing_slip_is_completed :
        MongoDbTestFixture
    {
        [Test]
        public async Task Should_complete_the_activity()
        {
            ConsumeContext<RoutingSlipActivityCompleted> context = await _prepareCompleted;

            Assert.AreEqual(_trackingNumber, context.Message.TrackingNumber);

            Assert.IsTrue(context.CorrelationId.HasValue);
            Assert.AreNotEqual(_trackingNumber, context.CorrelationId.Value);
        }

        [Test]
        public async Task Should_complete_the_routing_slip()
        {
            ConsumeContext<RoutingSlipCompleted> context = await _completed;

            Assert.AreEqual(_trackingNumber, context.Message.TrackingNumber);

            Assert.IsTrue(context.CorrelationId.HasValue);
            Assert.AreEqual(_trackingNumber, context.CorrelationId.Value);
        }

        [Test]
        public async Task Should_complete_the_second_activity()
        {
            ConsumeContext<RoutingSlipActivityCompleted> context = await _sendCompleted;

            Assert.AreEqual(_trackingNumber, context.Message.TrackingNumber);

            Assert.IsTrue(context.CorrelationId.HasValue);
            Assert.AreNotEqual(_trackingNumber, context.CorrelationId.Value);
        }

        [Test]
        public async Task Should_upsert_the_event_into_the_routing_slip()
        {
            await _completed;
            await Task.Delay(2000);
            await _prepareCompleted;
            await Task.Delay(2000);
            await _sendCompleted;
            await Task.Delay(2000);

            FilterDefinition<RoutingSlipDocument> query = Builders<RoutingSlipDocument>.Filter.Eq(x => x.TrackingNumber, _trackingNumber);

            var routingSlip = await (await _collection.FindAsync(query).ConfigureAwait(false)).SingleOrDefaultAsync().ConfigureAwait(false);

            Assert.IsNotNull(routingSlip);
            Assert.IsNotNull(routingSlip.Events);
            Assert.AreEqual(3, routingSlip.Events.Length);
        }

        [OneTimeSetUp]
        public async Task Setup()
        {
            _trackingNumber = NewId.NextGuid();

            Console.WriteLine("Tracking Number: {0}", _trackingNumber);

            await Bus.Publish<RoutingSlipCompleted>(new RoutingSlipCompletedEvent(_trackingNumber, DateTime.UtcNow, TimeSpan.FromSeconds(1)));

            await Bus.Publish<RoutingSlipActivityCompleted>(new RoutingSlipActivityCompletedEvent(_trackingNumber,
                "Prepare",
                NewId.NextGuid(), DateTime.UtcNow));

            await Bus.Publish<RoutingSlipActivityCompleted>(new RoutingSlipActivityCompletedEvent(_trackingNumber, "Send",
                NewId.NextGuid(), DateTime.UtcNow));
        }

        protected override void ConfigureBus(IInMemoryBusFactoryConfigurator configurator)
        {
            base.ConfigureBus(configurator);

            configurator.ConfigureRoutingSlipEventCorrelation();
        }

        protected override void ConfigureInputQueueEndpoint(IInMemoryReceiveEndpointConfigurator configurator)
        {
            base.ConfigureInputQueueEndpoint(configurator);

            _collection = Database.GetCollection<RoutingSlipDocument>(EventCollectionName);

            var persister = new RoutingSlipEventPersister(_collection);

            configurator.UseRetry(Retry.Selected<MongoWriteException>().Interval(10, TimeSpan.FromMilliseconds(20)));

            var partitioner = configurator.CreatePartitioner(16);

            configurator.RoutingSlipEventConsumers(persister, partitioner);
            configurator.RoutingSlipActivityEventConsumers(persister, partitioner);

            _completed = Handled<RoutingSlipCompleted>(configurator);
            _prepareCompleted = Handled<RoutingSlipActivityCompleted>(configurator, x => x.Message.ActivityName == "Prepare");
            _sendCompleted = Handled<RoutingSlipActivityCompleted>(configurator, x => x.Message.ActivityName == "Send");
        }

        IMongoCollection<RoutingSlipDocument> _collection;
        Guid _trackingNumber;
        Task<ConsumeContext<RoutingSlipCompleted>> _completed;
        Task<ConsumeContext<RoutingSlipActivityCompleted>> _prepareCompleted;
        Task<ConsumeContext<RoutingSlipActivityCompleted>> _sendCompleted;

        protected override void SetupActivities()
        {
        }
    }
}