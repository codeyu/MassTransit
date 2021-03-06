﻿// Copyright 2007-2011 Chris Patterson, Dru Sellers, Travis Smith, et. al.
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
namespace MassTransit.Testing.Instances
{
	using System;
	using System.Collections.Generic;
	using Magnum.Extensions;
	using Scenarios;
	using TestActions;

	public abstract class BusTestInstance
	{
		readonly IList<TestAction> _actions;
		readonly BusTestScenario _scenario;
		bool _disposed;

		protected BusTestInstance(BusTestScenario scenario, IList<TestAction> actions)
		{
			_scenario = scenario;
			_actions = actions;
		}

		public ReceivedMessageList Received
		{
			get { return _scenario.Received; }
		}

		public SentMessageList Sent
		{
			get { return _scenario.Sent; }
		}

		public ReceivedMessageList Skipped
		{
			get { return _scenario.Skipped; }
		}

		public BusTestScenario Scenario
		{
			get { return _scenario; }
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (_disposed) return;
			if (disposing)
			{
				_scenario.Dispose();
			}

			_disposed = true;
		}

		protected void ExecuteTestActions()
		{
			_actions.Each(x => x.Act(_scenario.Bus));
		}

		~BusTestInstance()
		{
			Dispose(false);
		}
	}
}