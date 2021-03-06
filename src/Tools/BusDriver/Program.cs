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
namespace BusDriver
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using log4net;
	using log4net.Appender;
	using log4net.Config;
	using log4net.Core;
	using log4net.Filter;
	using log4net.Layout;
	using Magnum.CommandLineParser;
	using Magnum.Extensions;
	using MassTransit;
	using MassTransit.Transports.Loopback;
	using MassTransit.Transports.Msmq;
	using MassTransit.Transports.RabbitMq;

	class Program
	{
		static readonly ILog _log = LogManager.GetLogger(typeof (Program));
		static readonly MonadicCommandLineParser _parser = new MonadicCommandLineParser();
		static ConsoleAppender _appender;
		static IServiceBus _bus;
		static Uri _driverUri = new Uri("msmq://localhost/masstransit_busdriver");
		static IList<IPendingCommand> _pending;

		public static IServiceBus Bus
		{
			get
			{
				if (_bus == null)
				{
					_log.DebugFormat("Starting service bus instance on {0}", _driverUri);

					_bus = ServiceBusFactory.New(x =>
						{
							x.UseMsmq();
							x.UseRabbitMq();
							x.UseXmlSerializer();
							x.ReceiveFrom(_driverUri);
						});
				}

				return _bus;
			}
		}

		public static string CurrentUri { get; set; }
		public static TransportCache Transports { get; private set; }

		public static void AddPendingCommand(IPendingCommand command)
		{
			_pending.Add(command);
		}

		static void Main()
		{
			BootstrapLogger();

			_pending = new List<IPendingCommand>();

			try
			{
				Transports = new TransportCache();
				Transports.AddTransportFactory(new LoopbackTransportFactory());
				Transports.AddTransportFactory(new MsmqTransportFactory());
				Transports.AddTransportFactory(new RabbitMqTransportFactory());

				string line = CommandLine.GetUnparsedCommandLine();
				if (line.IsNotEmpty())
				{
					ProcessLine(line);
				}
				else
				{
					_appender.Threshold = Level.All;
					RunInteractiveConsole();
				}
			}
			finally
			{
				WaitForPendingCommands();

				if (_bus != null)
				{
					_log.Debug("Disposing of service bus instance");
					_bus.Dispose();
					_bus = null;
				}

				Transports.Dispose();
				Transports = null;

				_log.Debug("End of Line.");
			}
		}

		static void WaitForPendingCommands()
		{
			var exit = new ManualResetEvent(false);

			try
			{
				Console.CancelKeyPress += (sender, args) =>
					{
						if (args.SpecialKey == ConsoleSpecialKey.ControlBreak)
							return;

						args.Cancel = true;

						_log.Info("Control+C detected, exiting without waiting");

						exit.Set();
					};

				foreach (IPendingCommand command in _pending)
				{
					try
					{
						_log.DebugFormat("Waiting for {0} to complete", command.Description);

						var handles = new[] {exit, command.WaitHandle};

						int result = WaitHandle.WaitAny(handles, 30.Seconds());
						if (result == WaitHandle.WaitTimeout)
							throw new TimeoutException("Timeout waiting for " + command.Description);
					}
					catch (Exception ex)
					{
						_log.Error("Exception while waiting for " + command.Description, ex);
					}
				}
			}
			catch (Exception ex)
			{
				_log.Error("Exception while waiting for pending commands to complete", ex);
			}
			finally
			{
				exit.Close();
			}
		}

		static void RunInteractiveConsole()
		{
			_log.DebugFormat("BusDriver v{0}, .NET Framework v{1}", typeof (Program).Assembly.GetName().Version,
				Environment.Version);

			_log.Debug("Starting interactive console");
			_log.Debug("Enter help for a list of commands, or exit to, well, to exit.");

			bool keepGoing = true;
			do
			{
				string line = "";
				try
				{
					Console.Write("> ");
					Console.Out.Flush();

					line = Console.ReadLine();
					if (line.Trim().Length == 0)
						continue;

					keepGoing = ProcessLine(line);
				}
				catch (Exception ex)
				{
					_log.Error("Exception processing: " + (line ?? ""), ex);
				}
			} while (keepGoing);
		}

		static bool ProcessLine(string line)
		{
			return CommandParser.Parse(line);
		}

		static void BootstrapLogger()
		{
			_appender = new ConsoleAppender();
			_appender.Threshold = Level.Info;
			_appender.Layout = new PatternLayout("%m%n");

			var filter = new LoggerMatchFilter();
			filter.AcceptOnMatch = false;
			filter.LoggerToMatch = "MassTransit";

			_appender.AddFilter(filter);


			BasicConfigurator.Configure(_appender);
		}
	}
}