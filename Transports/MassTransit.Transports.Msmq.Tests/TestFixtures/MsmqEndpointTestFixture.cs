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
namespace MassTransit.Transports.Msmq.Tests.TestFixtures
{
	using Configuration;
	using Internal;
	using MassTransit.Tests.TextFixtures;
	using Rhino.Mocks;
	using Services.Subscriptions;

	public class MsmqEndpointTestFixture :
		EndpointTestFixture<MsmqEndpoint>
	{
		public string LocalEndpointUri { get; private set; }
		public string LocalErrorUri { get; private set; }
		public string RemoteEndpointUri { get; private set; }

		public ISubscriptionService SubscriptionService { get; private set; }
		public IServiceBus LocalBus { get; private set; }
		public IServiceBus RemoteBus { get; private set; }

		protected override void EstablishContext()
		{
			base.EstablishContext();

			LocalEndpointUri = "msmq://localhost/mt_client";
			LocalErrorUri = "msmq://localhost/mt_client_error";
			RemoteEndpointUri = "msmq://localhost/mt_server";

			LocalEndpoint = new MsmqEndpoint(LocalEndpointUri);
			LocalErrorEndpoint = new MsmqEndpoint(LocalErrorUri);
			RemoteEndpoint = new MsmqEndpoint(RemoteEndpointUri);

			SetupSubscriptionService();

			LocalBus = ServiceBusConfigurator.New(x =>
				{
					x.AddService<SubscriptionPublisher>();
					x.AddService<SubscriptionConsumer>();
					x.ReceiveFrom(LocalEndpointUri);
					x.SendErrorsTo(LocalErrorUri);
				});

			RemoteBus = ServiceBusConfigurator.New(x =>
				{
					x.AddService<SubscriptionPublisher>();
					x.AddService<SubscriptionConsumer>();
					x.ReceiveFrom(RemoteEndpointUri);
				});
		}


		private void SetupSubscriptionService()
		{
			SubscriptionService = new LocalSubscriptionService();
			ObjectBuilder.Stub(x => x.GetInstance<IEndpointSubscriptionEvent>())
				.Return(SubscriptionService);

			ObjectBuilder.Stub(x => x.GetInstance<SubscriptionPublisher>())
				.Return(null)
				.WhenCalled(invocation =>
				{
					// Return a unique instance of this class
					invocation.ReturnValue = new SubscriptionPublisher(SubscriptionService);
				});

			ObjectBuilder.Stub(x => x.GetInstance<SubscriptionConsumer>())
				.Return(null)
				.WhenCalled(invocation =>
				{
					// Return a unique instance of this class
					invocation.ReturnValue = new SubscriptionConsumer(SubscriptionService, EndpointFactory);
				});
		}



		public MsmqEndpoint LocalEndpoint { get; private set; }
		public MsmqEndpoint LocalErrorEndpoint { get; private set; }
		public MsmqEndpoint RemoteEndpoint { get; private set; }

		protected override void TeardownContext()
		{
			LocalBus.Dispose();
			LocalBus = null;

			RemoteBus.Dispose();
			RemoteBus = null;

			LocalEndpoint.Dispose();
			LocalEndpoint = null;

			LocalErrorEndpoint.Dispose();
			LocalErrorEndpoint = null;

			base.TeardownContext();
		}
	}
}