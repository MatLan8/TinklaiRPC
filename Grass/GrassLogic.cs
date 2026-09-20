namespace Servers;

using NLog;

using Services;


/// <summary>
/// Single meadow patch.
/// </summary>
class GrassPatch
{
	/// <summary>
	/// Bugs currently on this patch.
	/// </summary>
	public List<BugState> Bugs = [];

	/// <summary>
	/// Bird currently on this patch, if any.
	/// </summary>
	public BirdState? Bird;
}


/// <summary>
/// Server-side bug state.
/// </summary>
class BugState
{
	/// <summary>
	/// Bug ID.
	/// </summary>
	public int Id;

	/// <summary>
	/// Current mass.
	/// </summary>
	public int Size;

	/// <summary>
	/// True if the bug was eaten since the last client poll.
	/// </summary>
	public bool WasEaten;
}


/// <summary>
/// Server-side bird state.
/// </summary>
class BirdState
{
	/// <summary>
	/// Bird ID.
	/// </summary>
	public int Id;

	/// <summary>
	/// Current mass.
	/// </summary>
	public int Size;

	/// <summary>
	/// True if the bird was shot since the last client poll.
	/// </summary>
	public bool WasShot;
}


/// <summary>
/// Shared meadow state descriptor.
/// </summary>
class MeadowState
{
	/// <summary>
	/// Access lock.
	/// </summary>
	public readonly object AccessLock = new();

	/// <summary>
	/// Last unique bug ID value generated.
	/// </summary>
	public int LastUniqueBugId;

	/// <summary>
	/// Last unique bird ID value generated.
	/// </summary>
	public int LastUniqueBirdId;

	/// <summary>
	/// Patches of the meadow. Length is PlacesCount.
	/// </summary>
	public GrassPatch[] Patches = [];

	/// <summary>
	/// Bug ID to current patch index.
	/// </summary>
	public Dictionary<int, int> BugPlace = new();

	/// <summary>
	/// Bird ID to current patch index.
	/// </summary>
	public Dictionary<int, int> BirdPlace = new();
}


/// <summary>
/// <para>Meadow logic.</para>
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

	/// <summary>
	/// State descriptor.
	/// </summary>
	private MeadowState mMeadow = new MeadowState();

	/// <summary>
	/// Number of patches on the meadow.
	/// </summary>
	private const int PlacesCount = 10;

	/// <summary>
	/// Exclusive upper bound for random bug growth per move.
	/// </summary>
	private const int MaxGrowSize = 100;

	/// <summary>
	/// Constructor.
	/// </summary>
	public GrassLogic()
	{
		//initialize empty patches
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
	/// Get next unique bug ID from the server.
	/// </summary>
	/// <returns>Unique bug ID.</returns>
	public int GetUniqueBugId()
	{
		lock( mMeadow.AccessLock )
		{
			mMeadow.LastUniqueBugId += 1;
			return mMeadow.LastUniqueBugId;
		}
	}

	/// <summary>
	/// Get next unique bird ID from the server.
	/// </summary>
	/// <returns>Unique bird ID.</returns>
	public int GetUniqueBirdId()
	{
		lock( mMeadow.AccessLock )
		{
			mMeadow.LastUniqueBirdId += 1;
			return mMeadow.LastUniqueBirdId;
		}
	}

	/// <summary>
	/// Get current meadow occupancy snapshot.
	/// </summary>
	/// <returns>Bug counts and bird occupancy per patch.</returns>
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

	/// <summary>
	/// Spawn a bug on a random patch.
	/// </summary>
	/// <param name="bug">Bug to spawn.</param>
	/// <returns>Spawn result descriptor.</returns>
	public BugMoveAttemptDesc SpawnBug(BugDesc bug)
	{
		lock (mMeadow.AccessLock)
		{
			int bugId = bug.BugId;

			//already on meadow? deny
			if (mMeadow.BugPlace.ContainsKey(bugId))
			{
				return new BugMoveAttemptDesc
				{
					IsSuccess = false,
					Reason = "Bug already exists in the grass field",
					MovedTo = mMeadow.BugPlace[bugId],
				};
			}
			
			//find random patch to spawn the bug
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

	/// <summary>
	/// Spawn a bird on a random unoccupied patch.
	/// </summary>
	/// <param name="bird">Bird to spawn.</param>
	/// <returns>Spawn result descriptor.</returns>
	public BirdMoveAttemptDesc SpawnBird(BirdDesc bird)
	{
		lock (mMeadow.AccessLock)
		{
			int birdId = bird.BirdId;

			//already on meadow? deny
			if (mMeadow.BirdPlace.ContainsKey(birdId))
			{
				return new BirdMoveAttemptDesc
				{
					IsSuccess = false,
					Reason = "Bird already exists in the grass field.",
					MovedTo = mMeadow.BirdPlace[birdId],
				};
			}

			//keep at least one patch free for birds
			var birdsCount = mMeadow.BirdPlace.Count;
			if (birdsCount == PlacesCount - 1)
			{
				return new BirdMoveAttemptDesc
				{
					IsSuccess = false,
					Reason = "Grass already has maximum number of allowed birds.",
				};
			}

			//collect unoccupied patch indexes
			var freeSpots = new List<int>();
			for (int i = 0; i < PlacesCount; i++)
			{
				if (mMeadow.Patches[i].Bird == null)
					freeSpots.Add(i);
			}
			
			//find random free patch to spawn the bird
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

	/// <summary>
	/// Move a bug to the given patch and grow it.
	/// </summary>
	/// <param name="bug">Bug to move.</param>
	/// <param name="targetPatch">Target patch index.</param>
	/// <returns>Move result descriptor.</returns>
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

			//move the same bug object to the new patch
			oldPatch.Bugs.Remove(bugState);
			mMeadow.Patches[targetPatch].Bugs.Add(bugState);
			mMeadow.BugPlace[bugId] = targetPatch;

			//grow by a random non-negative amount
			bugState.Size += Random.Shared.Next(0, MaxGrowSize);

			mLog.Info($"Bug {bugId} moved to {targetPatch}, new size {bugState.Size}.");
			return new BugMoveAttemptDesc
			{
				IsSuccess = true,
				MovedTo = targetPatch,
				NewMass = bugState.Size
			};
		}
	}

	/// <summary>
	/// Move a bird to the given patch and eat the largest bug there, if any.
	/// </summary>
	/// <param name="bird">Bird to move.</param>
	/// <param name="targetPatch">Target patch index.</param>
	/// <returns>Move result descriptor.</returns>
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

			//target already taken by another bird? deny
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

			//vacate old patch and sit on target
			mMeadow.Patches[oldPlace].Bird = null;
			mMeadow.Patches[targetPatch].Bird = birdState;
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

			//eat the largest bug on this patch
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

	/// <summary>
	/// Respawn an eaten bug on a random patch. Caller must hold the meadow lock.
	/// </summary>
	/// <param name="bug">Eaten bug.</param>
	private void RespawnBug(BugState bug)
	{
		var oldPlace = mMeadow.BugPlace[bug.Id];
		mMeadow.Patches[oldPlace].Bugs.Remove(bug);

		var newPlace = Random.Shared.Next(PlacesCount);

		//reset growth and mark event for client poll
		bug.Size = 1;
		bug.WasEaten = true;

		mMeadow.Patches[newPlace].Bugs.Add(bug);
		mMeadow.BugPlace[bug.Id] = newPlace;

		mLog.Info($"Bug {bug.Id} has been eaten and respawned at {newPlace}.");
	}

	/// <summary>
	/// Respawn a shot bird on a random unoccupied patch. Caller must hold the meadow lock.
	/// </summary>
	/// <param name="bird">Shot bird.</param>
	private void RespawnBird(BirdState bird)
	{
		var oldPlace = mMeadow.BirdPlace[bird.Id];
		mMeadow.Patches[oldPlace].Bird = null;

		//collect unoccupied patch indexes
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

		//reset growth
		bird.Size = 1;

		mMeadow.Patches[newPlace].Bird = bird;
		mMeadow.BirdPlace[bird.Id] = newPlace;

		mLog.Info($"Bird {bird.Id} was shot and respawned at patch {newPlace}.");
	}

	/// <summary>
	/// Poll whether the bug was eaten since the last check.
	/// </summary>
	/// <param name="bug">Bug to check.</param>
	/// <returns>Status descriptor.</returns>
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

			//consume the event so it is reported only once
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

	/// <summary>
	/// Poll whether the bird was shot since the last check.
	/// </summary>
	/// <param name="bird">Bird to check.</param>
	/// <returns>Status descriptor.</returns>
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

			//consume the event so it is reported only once
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
	/// Background task. Periodically decides whether to shoot the largest bird.
	/// </summary>
	private void BackgroundTask()
	{
		while( true )
		{
			//sleep a while
			Thread.Sleep(1000 + Random.Shared.Next(1000));

			//randomly decide whether to shoot
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
