namespace Servers;

using System.Security.Cryptography;
using NLog;

using Services;

class GrassPatch
{
	public List<BugState> Bugs = [];
	public BirdState? Bird;
}

class BugState
{
	public int Id;
	
	public int Size;
}

class BirdState
{
	public int Id;
	
	public int Size;
}

class MeadowState
{
	public readonly object AccessLock = new();
	
	public int LastUniqueBugId;
	
	public int LastUniqueBirdId;
	
	public GrassPatch[] Patches = [];

	public Dictionary<int, int> BugPlace = new();

	public Dictionary<int, int> BirdPlace = new();
}




/// <summary>
/// <para>Traffic light logic.</para>
/// <para>Thread safe.</para>
/// </summary>
class GrassLogic
{
	/// <summary>
	/// Logger for this class.
	/// </summary>
	private Logger mLog = LogManager.GetCurrentClassLogger();

	/// <summary>
	/// Background task thread.
	/// </summary>
	private Thread mBgTaskThread;
	
	private MeadowState mMeadow = new MeadowState();


	private const int PlacesCount = 10;

	private const int MaxGrowSize = 100;
	
	/// <summary>
	/// Constructor.
	/// </summary>
	public GrassLogic()
	{
		
		mMeadow.Patches = new GrassPatch[PlacesCount];
		for (int i = 0; i < PlacesCount; i++)
		{
			mMeadow.Patches[i] = new GrassPatch();
		}
		
		//start the background task
		// mBgTaskThread = new Thread(BackgroundTask);
		// mBgTaskThread.Start();
		
	}

	/// <summary>
	/// Get next unique ID from the server. Is used by cars to acquire client ID's.
	/// </summary>
	/// <returns>Unique ID.</returns>
	public int GetUniqueBugId() 
	{
		lock( mMeadow.AccessLock )
		{
			mMeadow.LastUniqueBugId += 1;
			return mMeadow.LastUniqueBugId;
		}
	}
	
	public int GetUniqueBirdId() 
	{
		lock( mMeadow.AccessLock )
		{
			mMeadow.LastUniqueBirdId += 1;
			return mMeadow.LastUniqueBirdId;
		}
	}


	public MeadowSnapshot GetMeadow()
	{
		lock (mMeadow.AccessLock)
		{
			var bugCounts = new int[PlacesCount];
			var birdOccupied = new bool[PlacesCount];
			
			for (var i = 0; i < PlacesCount; i++)
			{
				bugCounts[i] = mMeadow.Patches[i].Bugs.Count;
				birdOccupied[i] = mMeadow.Patches[i].Bird != null;
			}
			return new MeadowSnapshot
			{
				BugCounts = bugCounts,
				BirdOccupied = birdOccupied,
			};
		}
	}
	
	public BugMoveAttemptDesc SpawnBug(BugDesc bug)
	{
		lock (mMeadow.AccessLock)
		{
			int bugId = bug.BugId;
			
			if (mMeadow.BugPlace.ContainsKey(bugId))
			{
				return new BugMoveAttemptDesc
				{
					IsSuccess = false,
					Reason = "Bug already joined",
					MovedTo = mMeadow.BugPlace[bugId],
				};
			}
			
			int place = Random.Shared.Next(PlacesCount);
			
			var bugState = new BugState { Id = bugId, Size = 0 };
			
			mMeadow.Patches[place].Bugs.Add(bugState);
			
			mMeadow.BugPlace[bugId] = place;
			
			mLog.Info($"Bug {bugId} spawned on patch {place}.");

			return new BugMoveAttemptDesc
			{
				IsSuccess = true,
				MovedTo = place,
				NewMass = 1,
			};
		}
	}
	
	public BirdMoveAttemptDesc SpawnBird(BirdDesc bird)
	{
		lock (mMeadow.AccessLock)
		{
			int birdId = bird.BirdId;
			
			if (mMeadow.BirdPlace.ContainsKey(birdId))
			{
				return new BirdMoveAttemptDesc
				{
					IsSuccess = false,
					Reason = "Bird already exists in the grass field.",
					MovedTo = mMeadow.BirdPlace[birdId],
				};
			}
			
			var freeSpots = new List<int>();
			
			for (int i = 0; i < PlacesCount; i++)
			{
				if (mMeadow.Patches[i].Bird == null)
					freeSpots.Add(i);
			}
			if (freeSpots.Count == 0)
			{
				mLog.Info($"Bird {birdId} failed to respawn because all the grass patches are taken.");
				return new BirdMoveAttemptDesc
				{
					IsSuccess = false,
					Reason = "All grass patches are taken.",
				};
			}
			int place = freeSpots[Random.Shared.Next(freeSpots.Count)];


			var birdState = new BirdState
			{
				Id = birdId,
				Size = 1,
			};

			mMeadow.Patches[place].Bird = birdState;
			
			mMeadow.BirdPlace[birdId] = place;
			
			mLog.Info($"Bird {birdId} spawned on patch {place}.");

			return new BirdMoveAttemptDesc
			{
				IsSuccess = true,
				MovedTo = place,
				NewMass = 1,
			};
		}
	}


	public BugMoveAttemptDesc MoveBug(BugDesc bug, int targetPatch)
	{
		lock (mMeadow.AccessLock)
		{
			int bugId = bug.BugId;
			if (!mMeadow.BugPlace.TryGetValue(bugId, out int oldPlace))
			{
				return new BugMoveAttemptDesc
				{
					IsSuccess = false,
					Reason = "Bug isn't in grass field yet.",
					MovedTo = -1,
					NewMass = 1,
				};
			}
			
			var oldPatch = mMeadow.Patches[oldPlace];
			
			// 2. Find the actual BugState in the old patch list
			var bugState = oldPatch.Bugs.First(b => b.Id == bugId);
			
			// 3. Remove from old patch
			oldPatch.Bugs.Remove(bugState);
			
			// 4. Add to new patch (same object, not a new BugState)
			mMeadow.Patches[targetPatch].Bugs.Add(bugState);
			
			// 5. Update dictionary
			mMeadow.BugPlace[bugId] = targetPatch;
			
			// 6. Grow size
			bugState.Size += Random.Shared.Next(0, MaxGrowSize);
			
			mLog.Info($"Bug {bugId} moved to {targetPatch}.");
			return new BugMoveAttemptDesc
			{
				MovedTo = targetPatch,
				NewMass = bugState.Size
			};
		}
	}
	
	
	public BirdMoveAttemptDesc MoveBird(BirdDesc bird, int targetPatch)
	{
		lock (mMeadow.AccessLock)
		{
			int birdId = bird.BirdId;
			if (!mMeadow.BirdPlace.TryGetValue(birdId, out int oldPlace))
			{
				mLog.Info($"Bird {bird.BirdId} failed to move to {targetPatch}, because it wasnt in the grass field yet..");
				return new BirdMoveAttemptDesc
				{
					IsSuccess = false,
					Reason = "Bird isn't in grass field yet.",
					MovedTo = -1,
					NewMass = 1,
				};
			}

			if (mMeadow.Patches[targetPatch].Bird != null)
			{
				mLog.Info($"Bird {bird.BirdId} failed to move to {targetPatch}, because other bird already occupied the patch.");
				return new BirdMoveAttemptDesc
				{
					IsSuccess = false,
					Reason = $"Another bird is already at patch {targetPatch}.",
					MovedTo = -1,
					NewMass = 1,
				};
			}
			var birdState = mMeadow.Patches[oldPlace].Bird;
			mMeadow.Patches[oldPlace].Bird = null;
			
			mMeadow.Patches[targetPatch].Bird = birdState;
			
			// 5. Update dictionary
			mMeadow.BirdPlace[birdId] = targetPatch;
			
			var patchBugs = mMeadow.Patches[targetPatch].Bugs;

			if (patchBugs.Count == 0)
			{
				mLog.Info($"Bird {bird.BirdId} moved to {targetPatch}, no bugs were found.");
				return new BirdMoveAttemptDesc
				{
					IsSuccess = true,
					AteBug = false,
					MovedTo = targetPatch,
					NewMass = birdState.Size,
				};
			}

			var largestBug = patchBugs.OrderByDescending(b => b.Size).First();
			birdState.Size += largestBug.Size;	
			
			RespawnBug(largestBug);
			
			mLog.Info($"Bird {bird.BirdId} moved to {targetPatch} and ate bug {largestBug.Id}.");
			
			
			return new BirdMoveAttemptDesc
			{
				IsSuccess = true,
				AteBug = true,
				MovedTo = targetPatch,
				NewMass = birdState.Size,
			};
		}
	}

	private void RespawnBug(BugState bug)
	{
		int oldPlace = mMeadow.BugPlace[bug.Id];
		mMeadow.Patches[oldPlace].Bugs.Remove(bug);
		
		int newPlace = Random.Shared.Next(PlacesCount);
		
		bug.Size = 1;
		
		mMeadow.Patches[newPlace].Bugs.Add(bug);
		mMeadow.BugPlace[bug.Id] = newPlace;
		
		mLog.Info($"Bug {bug.Id} has been eaten and respawned at {newPlace}.");
	}
	

	// /// <summary>
	// /// Background task for the traffic light.
	// /// </summary>
	// public void BackgroundTask()
	// {
	// 	//initialize random number generator
	// 	var rnd = new Random();
	//
	// 	//
	// 	while( true )
	// 	{
	// 		//sleep a while
	// 		Thread.Sleep(500 + rnd.Next(1500));
	//
	// 		//switch the light
	// 		lock( mState.AccessLock )
	// 		{
	// 			mState.LightState = 
	// 				mState.LightState == LightState.Red 
	// 				? LightState.Green 
	// 				: LightState.Red;
	//
	// 			var colorCode = mState.LightState switch
	// 			{
	// 				LightState.Red => "\e[31m",
	// 				LightState.Green => "\e[32m"
	// 			};
	// 			var endColor = "\e[0m";
	//
	// 			mLog.Info($"New light state is '{colorCode}{mState.LightState}{endColor}'.");
	// 		}
	// 	}
	// }
}