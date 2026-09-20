namespace Clients;

using Microsoft.Extensions.DependencyInjection;

using SimpleRpc.Serialization.Hyperion;
using SimpleRpc.Transports;
using SimpleRpc.Transports.Http.Client;

using NLog;

using Services;


/// <summary>
/// Bird client.
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

				//initialize bird descriptor
				var bird = new BirdDesc
				{
					BirdId = grass.GetUniqueBirdId()
				};

				Console.Title = $"I am bird {bird.BirdId}";

				//retry spawn until a free patch is available
				while (true)
				{
					var joinAttempt = grass.SpawnBird(bird);
					if (!joinAttempt.IsSuccess)
					{
						mLog.Info($"I failed to spawn because {joinAttempt.Reason}");
						Thread.Sleep(4000 + rng.Next(1000));
						continue;
					}

					mLog.Info($"I am bird {bird.BirdId}, I have spawned at {joinAttempt.MovedTo} with mass {joinAttempt.NewMass}");
					break;
				}

				//do the bird stuff
				while (true)
				{
					Thread.Sleep(500 + rng.Next(1500));

					//check if we were shot while idle
					var status = grass.GetBirdStatus(bird);
					if (status.Error)
					{
						mLog.Info("Server failed to locate the bird.");
						continue;
					}
					if (status.WasKilled)
					{
						mLog.Info($"I was shot and have respawned at {status.NewPlace} patch.");
					}

					mLog.Info("I am looking for new grass patch to go to.");

					//get current grass field snapshot
					var meadow = grass.GetMeadow();
					
					//find the next spot we want to go to
					var nextSpot = GetNextSpot(meadow, rng);

					//retry a few times if the chosen patch was taken
					for (var i = 0; i < 5; i++)
					{
						var moveAttempt = grass.MoveBird(bird, nextSpot);
						if (!moveAttempt.IsSuccess)
						{
							mLog.Info($"I failed to move bird {moveAttempt.Reason}");
							meadow = grass.GetMeadow();
							nextSpot = GetNextSpot(meadow, rng);
						}
						else
						{
							if (moveAttempt.AteBug)
							{
								mLog.Info($"I have moved to patch {moveAttempt.MovedTo} and ate bug {moveAttempt.BugId}, my new mass: {moveAttempt.NewMass}.");
								break;
							}

							mLog.Info($"I have moved to patch {moveAttempt.MovedTo} however no bugs were present, my mass: {moveAttempt.NewMass}.");
							break;
						}
					}
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
	/// Choose next unoccupied patch with weight 1 + bug count. Empty patches still have a chance.
	/// </summary>
	/// <param name="meadow">Current meadow snapshot.</param>
	/// <param name="rng">Random number generator.</param>
	/// <returns>Chosen patch index.</returns>
	private int GetNextSpot(MeadowSnapshot meadow, Random rng)
	{
		int totalWeight = 0;
		for (int i = 0; i < meadow.BugCounts.Length; i++)
		{
			//skip patches occupied by another bird
			if (meadow.BirdOccupied[i])
				continue;
			totalWeight += 1 + meadow.BugCounts[i];
		}

		//roll random number
		var roll = rng.Next(totalWeight);
		var running = 0;
		var targetSpot = -1;

		//find the targetSpot by increasing the "running" by weight until we exceed the roll
		//this implementation prioritizes patches with more bugs
		for (var i = 0; i < meadow.BugCounts.Length; i++)
		{
			if (meadow.BirdOccupied[i])
				continue;

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
