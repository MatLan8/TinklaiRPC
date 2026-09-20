namespace Clients;

using Microsoft.Extensions.DependencyInjection;

using SimpleRpc.Serialization.Hyperion;
using SimpleRpc.Transports;
using SimpleRpc.Transports.Http.Client;

using NLog;

using Services;


/// <summary>
/// Bug client.
/// </summary>
class Client
{
	/// <summary>
	/// Logger for this class.
	/// </summary>
	Logger mLog = LogManager.GetCurrentClassLogger();

	/// <summary>
	/// Configures logging subsystem.
	/// </summary>
	private void ConfigureLogging()
	{
		var config = new NLog.Config.LoggingConfiguration();

		var console =
			new NLog.Targets.ConsoleTarget("console")
			{
				Layout = @"${date:format=HH\:mm\:ss}|${level}| ${message} ${exception}"
			};
		config.AddTarget(console);
		config.AddRuleForAllLevels(console);

		LogManager.Configuration = config;
	}

	/// <summary>
	/// Program body.
	/// </summary>
	private void Run() {
		//configure logging
		ConfigureLogging();

		//initialize random number generator
		var rng = new Random();

		//run everything in a loop to recover from connection errors
		while( true )
		{
			try {
				//connect to the server, get service client proxy
				var sc = new ServiceCollection();
				sc
					.AddSimpleRpcClient(
						"grassService",
						new HttpClientTransportOptions
						{
							Url = "http://127.0.0.1:5000/simplerpc",
							Serializer = "HyperionMessageSerializer"
						}
					)
					.AddSimpleRpcHyperionSerializer();

				sc.AddSimpleRpcProxy<IGrassService>("grassService");

				var sp = sc.BuildServiceProvider();
				var grass = sp.GetService<IGrassService>();

				//initialize bug descriptor
				var bug = new BugDesc
				{
					BugId = grass.GetUniqueBugId()
				};

				//spawn on the meadow
				var joinAttempt = grass.SpawnBug(bug);
				
				//if failed to spawn, because the bug already exists in the grass, exit
				if (!joinAttempt.IsSuccess)
				{
					mLog.Info($"I have failed to spawn | reason: {joinAttempt.Reason}.");
					return;
				}

				//log identity data
				mLog.Info($"I am bug {bug.BugId}, I have spawned at {joinAttempt.MovedTo} with mass {joinAttempt.NewMass}");
				Console.Title = $"I am bug {bug.BugId}";

				//do the bug stuff
				while (true)
				{
					Thread.Sleep(500 + rng.Next(1500));

					//check if we were eaten while idle
					var status = grass.GetBugStatus(bug);
					if (status.Error)
					{
						mLog.Info("Server failed to locate the bug.");
						continue;
					}
					if (status.WasKilled)
					{
						mLog.Info($"I was eaten and have respawned at {status.NewPlace} patch.");
					}

					mLog.Info("I am looking for new grass patch to go to.");

					//get grass field snapshot
					var meadow = grass.GetMeadow();
					
					//find the next spot
					var nextSpot = GetNextSpot(meadow, rng);

					//move to next spot
					var moveAttempt = grass.MoveBug(bug, nextSpot);
					if (!moveAttempt.IsSuccess)
					{
						mLog.Info($"I have failed to move to patch {nextSpot} | reason: {moveAttempt.Reason}.");
						continue;
					}
					mLog.Info($"I have moved to patch {moveAttempt.MovedTo}, my new mass: {moveAttempt.NewMass}.");
				}
			}
			catch( Exception e )
			{
				//log whatever exception to console
				mLog.Warn(e, "Unhandled exception caught. Will restart main loop.");

				//prevent console spamming
				Thread.Sleep(2000);
			}
		}
	}

	/// <summary>
	/// Choose next patch with weight 1 + bug count. Empty patches still have a chance.
	/// </summary>
	/// <param name="meadow">Current meadow snapshot.</param>
	/// <param name="rng">Random number generator.</param>
	/// <returns>Chosen patch index.</returns>
	private int GetNextSpot(MeadowSnapshot meadow, Random rng)
	{
		var totalWeight = 0;
		for (int i = 0; i < meadow.BugCounts.Length; i++)
		{
			totalWeight += 1 + meadow.BugCounts[i];
		}
		
		//roll random number
		var roll = rng.Next(totalWeight);
		var running = 0;
		var targetSpot = 0;

		//find the targetSpot by increasing the "running" by weight until we exceed the roll
		//this implementation prioritizes patches with more bugs
		for (var i = 0; i < meadow.BugCounts.Length; i++)
		{
			running += 1 + meadow.BugCounts[i];
			if (roll < running)
			{
				targetSpot = i;
				break;
			}
		}

		mLog.Info($"I decided to move to grass patch {targetSpot} (bugs there: {meadow.BugCounts[targetSpot]}, weight {1 + meadow.BugCounts[targetSpot]} / {totalWeight}).");
		return targetSpot;
	}

	/// <summary>
	/// Program entry point.
	/// </summary>
	/// <param name="args">Command line arguments.</param>
	static void Main(string[] args)
	{
		var self = new Client();
		self.Run();
	}
}
