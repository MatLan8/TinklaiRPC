#nullable enable
namespace Services;


/// <summary>
/// Bug descriptor.
/// </summary>
public class BugDesc
{
	/// <summary>
	/// Bug ID.
	/// </summary>
	public int BugId { get; set; }
}


/// <summary>
/// Bird descriptor.
/// </summary>
public class BirdDesc
{
	/// <summary>
	/// Bird ID.
	/// </summary>
	public int BirdId { get; set; }
}


/// <summary>
/// Descriptor of a bug spawn or move result.
/// </summary>
public class BugMoveAttemptDesc
{
	/// <summary>
	/// Indicates if the attempt has succeeded.
	/// </summary>
	public bool IsSuccess { get; set; }

	/// <summary>
	/// Patch index the bug ended on.
	/// </summary>
	public int MovedTo { get; set; }

	/// <summary>
	/// Bug mass after the attempt.
	/// </summary>
	public int NewMass { get; set; }

	/// <summary>
	/// If the attempt has failed, indicates the reason.
	/// </summary>
	public string? Reason { get; set; }
}


/// <summary>
/// Descriptor of a bird spawn or move result.
/// </summary>
public class BirdMoveAttemptDesc
{
	/// <summary>
	/// Indicates if the attempt has succeeded.
	/// </summary>
	public bool IsSuccess { get; set; }

	/// <summary>
	/// Indicates if a bug was eaten after moving.
	/// </summary>
	public bool AteBug { get; set; }

	/// <summary>
	/// ID of the eaten bug, if any.
	/// </summary>
	public int? BugId { get; set; }

	/// <summary>
	/// Patch index the bird ended on.
	/// </summary>
	public int MovedTo { get; set; }

	/// <summary>
	/// Bird mass after the attempt.
	/// </summary>
	public int NewMass { get; set; }

	/// <summary>
	/// If the attempt has failed, indicates the reason.
	/// </summary>
	public string? Reason { get; set; }
}


/// <summary>
/// Snapshot of meadow occupancy used by clients to choose a patch.
/// </summary>
public class MeadowSnapshot
{
	/// <summary>
	/// Bug count per patch. Index matches patch index.
	/// </summary>
	public int[] BugCounts;

	/// <summary>
	/// True if a bird currently occupies the patch. Index matches patch index.
	/// </summary>
	public bool[] BirdOccupied;
}


/// <summary>
/// Status of a client after eat or shoot events.
/// </summary>
public class StatusResponse
{
	/// <summary>
	/// True if the client could not be found on the meadow.
	/// </summary>
	public bool Error;

	/// <summary>
	/// True if the client was eaten or shot since the last poll.
	/// </summary>
	public bool WasKilled;

	/// <summary>
	/// Current patch index of the client.
	/// </summary>
	public int NewPlace;
}


/// <summary>
/// Service contract.
/// </summary>
public interface IGrassService
{
	/// <summary>
	/// Get next unique bug ID from the server.
	/// </summary>
	/// <returns>Unique bug ID.</returns>
	int GetUniqueBugId();

	/// <summary>
	/// Get next unique bird ID from the server.
	/// </summary>
	/// <returns>Unique bird ID.</returns>
	int GetUniqueBirdId();

	/// <summary>
	/// Get current meadow occupancy snapshot.
	/// </summary>
	/// <returns>Bug counts and bird occupancy per patch.</returns>
	MeadowSnapshot GetMeadow();

	/// <summary>
	/// Spawn a bug on a random patch.
	/// </summary>
	/// <param name="bug">Bug to spawn.</param>
	/// <returns>Spawn result descriptor.</returns>
	BugMoveAttemptDesc SpawnBug(BugDesc bug);

	/// <summary>
	/// Spawn a bird on a random unoccupied patch.
	/// </summary>
	/// <param name="bird">Bird to spawn.</param>
	/// <returns>Spawn result descriptor.</returns>
	BirdMoveAttemptDesc SpawnBird(BirdDesc bird);

	/// <summary>
	/// Move a bug to the given patch and grow it.
	/// </summary>
	/// <param name="bug">Bug to move.</param>
	/// <param name="targetPatch">Target patch index.</param>
	/// <returns>Move result descriptor.</returns>
	BugMoveAttemptDesc MoveBug(BugDesc bug, int targetPatch);

	/// <summary>
	/// Move a bird to the given patch and eat the largest bug there, if any.
	/// </summary>
	/// <param name="bird">Bird to move.</param>
	/// <param name="targetPatch">Target patch index.</param>
	/// <returns>Move result descriptor.</returns>
	BirdMoveAttemptDesc MoveBird(BirdDesc bird, int targetPatch);

	/// <summary>
	/// Poll whether the bug was eaten since the last check.
	/// </summary>
	/// <param name="bug">Bug to check.</param>
	/// <returns>Status descriptor.</returns>
	StatusResponse GetBugStatus(BugDesc bug);

	/// <summary>
	/// Poll whether the bird was shot since the last check.
	/// </summary>
	/// <param name="bird">Bird to check.</param>
	/// <returns>Status descriptor.</returns>
	StatusResponse GetBirdStatus(BirdDesc bird);
}
