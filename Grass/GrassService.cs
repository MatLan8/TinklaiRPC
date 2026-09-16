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

	public MeadowSnapshot GetMeadow()
	{
		return mLogic.GetMeadow();
	}


	public BugMoveAttemptDesc SpawnBug(BugDesc bug)
	{
		return mLogic.SpawnBug(bug);
	}
	
	public BirdMoveAttemptDesc SpawnBird(BirdDesc bird)
	{
		return mLogic.SpawnBird(bird);
	}
	
	public BugMoveAttemptDesc MoveBug(BugDesc bug, int targetPatch)
	{
		return mLogic.MoveBug(bug, targetPatch);
	}
	
	public BirdMoveAttemptDesc MoveBird(BirdDesc bird, int targetPatch)
	{
		return mLogic.MoveBird(bird, targetPatch);
	}
}