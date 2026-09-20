namespace Servers;

using Services;


/// <summary>
/// RPC service. Forwards contract methods to meadow logic.
/// </summary>
public class GrassService : IGrassService
{
	//NOTE: instance-per-request service would need logic to be static or injected from a singleton instance
	private readonly GrassLogic mLogic = new GrassLogic();

	/// <summary>
	/// Get next unique bug ID from the server.
	/// </summary>
	/// <returns>Unique bug ID.</returns>
	public int GetUniqueBugId()
	{
		return mLogic.GetUniqueBugId();
	}

	/// <summary>
	/// Get next unique bird ID from the server.
	/// </summary>
	/// <returns>Unique bird ID.</returns>
	public int GetUniqueBirdId()
	{
		return mLogic.GetUniqueBirdId();
	}

	/// <summary>
	/// Get current meadow occupancy snapshot.
	/// </summary>
	/// <returns>Bug counts and bird occupancy per patch.</returns>
	public MeadowSnapshot GetMeadow()
	{
		return mLogic.GetMeadow();
	}

	/// <summary>
	/// Spawn a bug on a random patch.
	/// </summary>
	/// <param name="bug">Bug to spawn.</param>
	/// <returns>Spawn result descriptor.</returns>
	public BugMoveAttemptDesc SpawnBug(BugDesc bug)
	{
		return mLogic.SpawnBug(bug);
	}

	/// <summary>
	/// Spawn a bird on a random unoccupied patch.
	/// </summary>
	/// <param name="bird">Bird to spawn.</param>
	/// <returns>Spawn result descriptor.</returns>
	public BirdMoveAttemptDesc SpawnBird(BirdDesc bird)
	{
		return mLogic.SpawnBird(bird);
	}

	/// <summary>
	/// Move a bug to the given patch and grow it.
	/// </summary>
	/// <param name="bug">Bug to move.</param>
	/// <param name="targetPatch">Target patch index.</param>
	/// <returns>Move result descriptor.</returns>
	public BugMoveAttemptDesc MoveBug(BugDesc bug, int targetPatch)
	{
		return mLogic.MoveBug(bug, targetPatch);
	}

	/// <summary>
	/// Move a bird to the given patch and eat the largest bug there, if any.
	/// </summary>
	/// <param name="bird">Bird to move.</param>
	/// <param name="targetPatch">Target patch index.</param>
	/// <returns>Move result descriptor.</returns>
	public BirdMoveAttemptDesc MoveBird(BirdDesc bird, int targetPatch)
	{
		return mLogic.MoveBird(bird, targetPatch);
	}

	/// <summary>
	/// Poll whether the bug was eaten since the last check.
	/// </summary>
	/// <param name="bug">Bug to check.</param>
	/// <returns>Status descriptor.</returns>
	public StatusResponse GetBugStatus(BugDesc bug)
	{
		return mLogic.GetBugStatus(bug);
	}

	/// <summary>
	/// Poll whether the bird was shot since the last check.
	/// </summary>
	/// <param name="bird">Bird to check.</param>
	/// <returns>Status descriptor.</returns>
	public StatusResponse GetBirdStatus(BirdDesc bird)
	{
		return mLogic.GetBirdStatus(bird);
	}
}
