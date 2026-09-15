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
/// Descriptor of pass atempt result
/// </summary>
public class MoveAttemptDesc
{
	public bool IsSuccess { get; set; }
	
	public int MovedTo { get; set; }

	/// <summary>
	/// If pass attempt has failed, indicates crash reason.
	/// </summary>
	public int NewMass { get; set; }
	
	public string? Reason { get; set; }
}


/// <summary>
/// Service contract.
/// </summary>
public interface IGrassService
{
	/// <summary>
	/// Get next unique ID from the server. Is used by cars to acquire client ID's.
	/// </summary>
	/// <returns>Unique ID.</returns>
	int GetUniqueBugId();
	
	int GetUniqueBirdId();


	int[] GetMeadow();
	
	MoveAttemptDesc SpawnBug(BugDesc bug);
	
	// MoveAttemptDesc SpawnBird(BirdDesc bird);
	
	MoveAttemptDesc MoveBug(BugDesc bug, int targetPatch);
}
