// Copyright 2007-2008 The Apache Software Foundation.
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
namespace MassTransit.Tests.Saga.StateMachine
{
	using System;
	using System.Linq;
	using Exceptions;
	using Locator;
	using Magnum.TestFramework;
	using MassTransit.Pipeline;
	using MassTransit.Pipeline.Inspectors;
	using MassTransit.Saga;
	using NUnit.Framework;
	using Rhino.Mocks;
	using TestFramework;
	using TextFixtures;

	[TestFixture]
	public class When_sagas_are_subscribed_to_the_service_bus :
		LoopbackTestFixture
	{
		protected override void EstablishContext()
		{
			base.EstablishContext();

			_sagaId = Guid.NewGuid();

			_repository = SetupSagaRepository<TestSaga>();

		    _initiateSimpleSagaUnsubscribeCalled = false;
		    _completeSimpleSagaUnsubscribeCalled = false;
		    _initiateSimpleSagaUnsubscribe = () => {_initiateSimpleSagaUnsubscribeCalled = true; return true; };
            _completeSimpleSagaUnsubscribe = () => { _completeSimpleSagaUnsubscribeCalled = true; return true; };

			_subscriptionEvent = MockRepository.GenerateMock<ISubscriptionEvent>();
			_subscriptionEvent.Expect(x => x.SubscribedTo<InitiateSimpleSaga>()).Repeat.Any().Return(_initiateSimpleSagaUnsubscribe);
			_subscriptionEvent.Expect(x => x.SubscribedTo<CompleteSimpleSaga>()).Repeat.Any().Return(_completeSimpleSagaUnsubscribe);

			LocalBus.InboundPipeline.Configure(x => x.Register(_subscriptionEvent));

			_remove = LocalBus.SubscribeSaga<TestSaga>(_repository);

			PipelineViewer.Trace(LocalBus.InboundPipeline);
		}

		private Guid _sagaId;
		private UnsubscribeAction _remove;
		private ISagaRepository<TestSaga> _repository;
		private ISubscriptionEvent _subscriptionEvent;
		private UnsubscribeAction _initiateSimpleSagaUnsubscribe;
		private UnsubscribeAction _completeSimpleSagaUnsubscribe;
	    private bool _initiateSimpleSagaUnsubscribeCalled;
	    private bool _completeSimpleSagaUnsubscribeCalled;

	    [Test, Ignore("Rhino Mock 3.6 Bug")]
		public void Should_publish_subscriptions_for_saga_subscriptions()
		{
			_subscriptionEvent.VerifyAllExpectations();
		}

        [Test, Ignore("Rhino Mock 3.6 Bug")]
        public void Should_remove_subscriptions_for_saga_subscriptions()
		{
			_remove();

			_subscriptionEvent.VerifyAllExpectations();

		    Assert.IsTrue(_initiateSimpleSagaUnsubscribeCalled);
            Assert.IsTrue(_completeSimpleSagaUnsubscribeCalled);
		}
	}

	[TestFixture]
	public class When_an_initiating_message_for_a_saga_arrives :
		LoopbackTestFixture
	{
		protected override void EstablishContext()
		{
			base.EstablishContext();

			_sagaId = Guid.NewGuid();

			_repository = SetupSagaRepository<TestSaga>();

			_remove = LocalBus.SubscribeSaga<TestSaga>(_repository);

			PipelineViewer.Trace(LocalBus.InboundPipeline);
		}

		private Guid _sagaId;
		private UnsubscribeAction _remove;
		private ISagaRepository<TestSaga> _repository;

		[Test]
		public void The_saga_should_be_created_when_an_initiating_message_is_received()
		{
			InitiateSimpleSaga message = new InitiateSimpleSaga(_sagaId);

			LocalBus.InboundPipeline.Dispatch(message);

			var saga = _repository.ShouldContainSaga(_sagaId);

			saga.WasInitiated.ShouldBeTrue();
		}

		[Test]
		public void The_saga_should_be_loaded_when_an_observed_message_is_received()
		{
			const string name = "Chris";

			LocalBus.InboundPipeline.Dispatch(new InitiateSimpleSaga(_sagaId) {Name = name});

			LocalBus.InboundPipeline.Dispatch(new ObservableSagaMessage {Name = name});

			var saga = _repository.ShouldContainSaga(_sagaId);

			saga.WasObserved.ShouldBeTrue();
		}

		[Test]
		public void The_saga_should_be_loaded_when_an_orchestrated_message_is_received()
		{
			LocalBus.InboundPipeline.Dispatch(new InitiateSimpleSaga(_sagaId));

			LocalBus.InboundPipeline.Dispatch(new CompleteSimpleSaga(_sagaId));

			var saga = _repository.ShouldContainSaga(_sagaId);

			saga.WasCompleted.ShouldBeTrue();
		}
	}

	[TestFixture]
	public class When_an_existing_saga_receives_an_initiating_message :
		LoopbackTestFixture
	{
		protected override void EstablishContext()
		{
			base.EstablishContext();

			_sagaId = Guid.NewGuid();

			_repository = SetupSagaRepository<TestSaga>();

			LocalBus.SubscribeSaga<TestSaga>(_repository);
		}

		private Guid _sagaId;
		private ISagaRepository<TestSaga> _repository;

		[Test]
		public void An_exception_should_be_thrown()
		{
			InitiateSimpleSaga message = new InitiateSimpleSaga(_sagaId);

			LocalBus.InboundPipeline.Dispatch(message);

			try
			{
				LocalBus.InboundPipeline.Dispatch(message);
			}
			catch (SagaException sex)
			{
				Assert.AreEqual(sex.MessageType, typeof (InitiateSimpleSaga));
			}
		}
	}
}