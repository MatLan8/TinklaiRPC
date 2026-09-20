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

	public bool WasEaten;
}

class BirdState
{
	public int Id;
	
	public int Size;

	public bool WasShot;
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
		mBgTaskThread = new Thread(BackgroundTask);
		mBgTaskThread.Start();
		
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
					Reason = "Bug already exists in the grass field",
					MovedTo = mMeadow.BugPlace[bugId],
				};
			}
			
			var place = Random.Shared.Next(PlacesCount);
			
			var bugState = new BugState { Id = bugId, Size = 1 };
			
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

			var birdsCount = mMeadow.BirdPlace.Count;
			if (birdsCount == PlacesCount - 1)
			{
				return new BirdMoveAttemptDesc
				{
					IsSuccess = false,
					Reason = "Grass already has maximum number of allowed birds.",
				};
			}
			var freeSpots = new List<int>();
			
			for (int i = 0; i < PlacesCount; i++)
			{
				if (mMeadow.Patches[i].Bird == null)
					freeSpots.Add(i);
			}
			
			var place = freeSpots[Random.Shared.Next(freeSpots.Count)];


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
			var bugState = oldPatch.Bugs.FirstOrDefault(b => b.Id == bugId);
			if (bugState == null)
			{
				return new BugMoveAttemptDesc
				{
					IsSuccess = false,
					Reason = "Server couldn't find the bug.",
					MovedTo = -1,
					NewMass = 1,
				};
			}
			
			// 3. Remove from old patch
			oldPatch.Bugs.Remove(bugState);
			
			// 4. Add to new patch (same object, not a new BugState)
			mMeadow.Patches[targetPatch].Bugs.Add(bugState);
			
			// 5. Update dictionary
			mMeadow.BugPlace[bugId] = targetPatch;
			
			// 6. Grow size
			bugState.Size += Random.Shared.Next(0, MaxGrowSize);
			
			mLog.Info($"Bug {bugId} moved to {targetPatch}, new size {bugState.Size}.");
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
			var birdId = bird.BirdId;
			if (!mMeadow.BirdPlace.TryGetValue(birdId, out var oldPlace))
			{
				mLog.Info($"Bird {bird.BirdId} failed to move to {targetPatch}, because it wasnt in the grass field yet.");
				return new BirdMoveAttemptDesc
				{
					IsSuccess = false,
					Reason = "Bird isn't in grass field yet.",
				};
			}

			if (mMeadow.Patches[targetPatch].Bird != null)
			{
				mLog.Info($"Bird {bird.BirdId} failed to move to {targetPatch}, because other bird already occupied the patch.");
				return new BirdMoveAttemptDesc
				{
					IsSuccess = false,
					Reason = $"Another bird is already at patch {targetPatch}.",
				};
			}
			var birdState = mMeadow.Patches[oldPlace].Bird;

			if (birdState == null)
			{
				return new BirdMoveAttemptDesc
				{
					IsSuccess = false,
					Reason = "Server couldn't find the bird.",
				};
			}
			
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
			
			mLog.Info($"Bird {bird.BirdId} moved to {targetPatch} and ate bug {largestBug.Id}, new size {birdState.Size}.");
			
			
			return new BirdMoveAttemptDesc
			{
				IsSuccess = true,
				AteBug = true,
				MovedTo = targetPatch,
				NewMass = birdState.Size,
				BugId = largestBug.Id
			};
		}
	}

	private void RespawnBug(BugState bug)
	{
		var oldPlace = mMeadow.BugPlace[bug.Id];
		mMeadow.Patches[oldPlace].Bugs.Remove(bug);
		
		var newPlace = Random.Shared.Next(PlacesCount);
		
		bug.Size = 1;
		bug.WasEaten = true;
		
		mMeadow.Patches[newPlace].Bugs.Add(bug);
		mMeadow.BugPlace[bug.Id] = newPlace;
		
		mLog.Info($"Bug {bug.Id} has been eaten and respawned at {newPlace}.");
	}
	
	private void RespawnBird(BirdState bird)
	{
		var oldPlace = mMeadow.BirdPlace[bird.Id];
		mMeadow.Patches[oldPlace].Bird = null;
		
		var freeSpots = new List<int>();
		for (int i = 0; i < PlacesCount; i++)
		{
			if (mMeadow.Patches[i].Bird == null)
				freeSpots.Add(i);
		}
		
		if (freeSpots.Count == 0)
		{
			mLog.Info($"Bird {bird.Id} cannot respawn — all patches occupied.");
			return;
		}
		
		var newPlace = freeSpots[Random.Shared.Next(freeSpots.Count)];
		
		bird.Size = 1;
		
		mMeadow.Patches[newPlace].Bird = bird;
		mMeadow.BirdPlace[bird.Id] = newPlace;
		
		mLog.Info($"Bird {bird.Id} was shot and respawned at patch {newPlace}.");
	}


	public StatusResponse GetBugStatus(BugDesc bug)
	{
		lock (mMeadow.AccessLock)
		{
			var bugId = bug.BugId;

			if (!mMeadow.BugPlace.TryGetValue(bugId, out var place))
			{
				return new StatusResponse
				{
					Error = true
				};
			}
			
			var bugState = mMeadow.Patches[place].Bugs.FirstOrDefault(b => b.Id == bugId);

			if (bugState == null)
			{
				return new StatusResponse
				{
					Error = true
				};
			}
			
			var wasEaten = bugState.WasEaten;
			
			bugState.WasEaten = false;

			return new StatusResponse
			{
				Error = false,
				WasKilled = wasEaten,
				NewPlace = place,
			};
		}
	}
	
	public StatusResponse GetBirdStatus(BirdDesc bird)
	{
		lock (mMeadow.AccessLock)
		{
			var birdId = bird.BirdId;

			if (!mMeadow.BirdPlace.TryGetValue(birdId, out var place))
			{
				return new StatusResponse
				{
					Error = true
				};
			}
			
			var birdState = mMeadow.Patches[place].Bird;

			if (birdState == null)
			{
				return new StatusResponse
				{
					Error = true
				};
			}
			
			var wasShot = birdState.WasShot;
			
			birdState.WasShot = false;

			return new StatusResponse
			{
				Error = false,
				WasKilled = wasShot,
				NewPlace = place,
			};
		}
	}
	
	

	/// <summary>
	/// Background task for the grass server.
	/// </summary>
	private void BackgroundTask()
	{
		while( true )
		{
			//sleep a while
			Thread.Sleep(1000 + Random.Shared.Next(1000));
			
			var randomValue = Random.Shared.Next(0, 5);

			if (randomValue == 0)
			{
				lock( mMeadow.AccessLock )
				{
					var biggestBird = mMeadow.Patches
						.Select(p => p.Bird)
						.Where(b => b != null)
						.MaxBy(b => b!.Size);
					
					if (biggestBird == null)
					{
						mLog.Info($"Server tried to shoot biggest bird, however no birds were found.");
					}
					else
					{
						biggestBird.WasShot = true;
						RespawnBird(biggestBird);
					}
				}
			}
		}
	}
}