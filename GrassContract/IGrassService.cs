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
/// Descriptor of pass atempt result
/// </summary>
public class BugMoveAttemptDesc
{
	public bool IsSuccess { get; set; }
	
	public int MovedTo { get; set; }

	/// <summary>
	/// If pass attempt has failed, indicates crash reason.
	/// </summary>
	public int NewMass { get; set; }
	
	public string? Reason { get; set; }
}


public class BirdMoveAttemptDesc
{
	public bool IsSuccess { get; set; }
	
	public bool AteBug { get; set; }
	
	public int? BugId { get; set; }
	
	public int MovedTo { get; set; }

	/// <summary>
	/// If pass attempt has failed, indicates crash reason.
	/// </summary>
	public int NewMass { get; set; }
	
	public string? Reason { get; set; }
}


public class MeadowSnapshot
{
	public int[] BugCounts;      // index = place, value = bug count
	public bool[] BirdOccupied;  // index = place, true = a bird is sitting there
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


	MeadowSnapshot GetMeadow();
	
	BugMoveAttemptDesc SpawnBug(BugDesc bug);
	
	BirdMoveAttemptDesc SpawnBird(BirdDesc bird);
	
	BugMoveAttemptDesc MoveBug(BugDesc bug, int targetPatch);
	
	BirdMoveAttemptDesc MoveBird(BirdDesc bird, int targetPatch);
	
}
