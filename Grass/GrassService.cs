namespace Servers;

using Services;

/// <summary>
/// Service
/// </summary>
public class GrassService : IGrassService
{
	//NOTE: instance-per-request service would need logic to be static or injected from a singleton instance
	private readonly GrassLogic mLogic = new GrassLogic();
	
	/// <summary>
	/// Get next unique ID from the server. Is used by cars to acquire client ID's.
	/// </summary>
	/// <returns>Unique ID.</returns>
	public int GetUniqueBugId() 
	{
		return mLogic.GetUniqueBugId();
	}

	public int GetUniqueBirdId()
	{
		return mLogic.GetUniqueBirdId();
	}

	public int[] GetMeadow()
	{
		return mLogic.GetMeadow();
	}


	public MoveAttemptDesc SpawnBug(BugDesc bug)
	{
		return mLogic.SpawnBug(bug);
	}
	
	// public MoveAttemptDesc SpawnBird(BirdDesc bird)
	// {
	// 	return mLogic.SpawnBird(bird);
	// }
	

	public MoveAttemptDesc MoveBug(BugDesc bug, int targetPatch)
	{
		return mLogic.MoveBug(bug, targetPatch);
	}
}