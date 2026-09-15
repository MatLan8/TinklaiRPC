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


	private const int PlacesCount = 250;

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


	public int[] GetMeadow()
	{
		lock (mMeadow.AccessLock)
		{
			var counts = new int[PlacesCount];
			for (int i = 0; i < PlacesCount; i++)
			{
				counts[i] = mMeadow.Patches[i].Bugs.Count;
			}
			return counts;
		}
	}
	
	public MoveAttemptDesc SpawnBug(BugDesc bug)
	{
		lock (mMeadow.AccessLock)
		{
			int bugId = bug.BugId;
			
			if (mMeadow.BugPlace.ContainsKey(bugId))
			{
				return new MoveAttemptDesc
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
			
			mLog.Info($"Bug {bugId} joined on patch {place}.");

			return new MoveAttemptDesc
			{
				IsSuccess = true,
				MovedTo = place,
				NewMass = 1,
			};
		}
	}
	


	public MoveAttemptDesc MoveBug(BugDesc bug, int targetPatch)
	{
		lock (mMeadow.AccessLock)
		{
			int bugId = bug.BugId;
			if (!mMeadow.BugPlace.TryGetValue(bugId, out int oldPlace))
			{
				return new MoveAttemptDesc
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
			return new MoveAttemptDesc
			{
				MovedTo = targetPatch,
				NewMass = bugState.Size
			};
		}
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