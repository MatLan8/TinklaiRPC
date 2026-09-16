namespace Clients;

using Microsoft.Extensions.DependencyInjection;

using SimpleRpc.Serialization.Hyperion;
using SimpleRpc.Transports;
using SimpleRpc.Transports.Http.Client;

using NLog;

using Services;


/// <summary>
/// Client example.
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
						"grassService", //must be same as on line 86
						new HttpClientTransportOptions
						{
							Url = "http://127.0.0.1:5000/simplerpc",
							Serializer = "HyperionMessageSerializer"
						}
					)
					.AddSimpleRpcHyperionSerializer();

				sc.AddSimpleRpcProxy<IGrassService>("grassService"); //must be same as on line 77

				var sp = sc.BuildServiceProvider();

				var grass = sp.GetService<IGrassService>();

				//initialize bug descriptor
				var bug = new BugDesc
				{
					//get unique client id
					BugId = grass.GetUniqueBugId()
				};

				var joinAttempt = grass.SpawnBug(bug);

				//log identity data
				mLog.Info($"I am bug {bug.BugId}, I have spawned at {joinAttempt.MovedTo} with mass {joinAttempt.NewMass}");
				
				Console.Title =
					$"I am bug {bug.BugId}";
					
				//do the bug stuff
				while (true)
				{
					Thread.Sleep(500 + rng.Next(1500));
					
					mLog.Info("I am looking for new grass patch to go to.");

					var meadow = grass.GetMeadow();
					var nextSpot = GetNextSpot(meadow, rng);

					var moveAttempt = grass.MoveBug(bug, nextSpot);
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


	private int GetNextSpot(MeadowSnapshot meadow, Random rng)
	{
		// weight[i] = 1 + bugs on patch i  (empty still has a chance)
		var totalWeight = 0;

		for (int i = 0; i < meadow.BugCounts.Length; i++)
		{
			totalWeight += 1 + meadow.BugCounts[i];
		}
		
		var roll = rng.Next(totalWeight);
		
		var running = 0;
		var targetSpot = 0;
		
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
