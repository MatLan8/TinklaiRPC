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
	public int GetUniqueId() 
	{
		return mLogic.GetUniqueId();
	}

	/// <summary>
	/// Get current light state.
	/// </summary>
	/// <returns>Current light state.</returns>				
	public LightState GetLightState()
	{
		return mLogic.GetLightState();
	}

	/// <summary>
	/// Queue give car at the light. Will only succeed if light is red.
	/// </summary>
	/// <param name="car">Bug to queue.</param>
	/// <returns>True on success, false on failure.</returns>
	public bool Queue(CarDesc car) 
	{
		return mLogic.Queue(car);
	}

	/// <summary>
	/// Tell if car is first in line in queue.
	/// </summary>
	/// <param name="carId">ID of the car to check for.</param>
	/// <returns>True if car is first in line. False if not first in line or not in queue.</returns>
	public bool IsFirstInLine(int carId)
	{
		return mLogic.IsFirstInLine(carId);
	}

	/// <summary>
	/// Try passing the traffic light. If car is in queue, it will be removed from it.
	/// </summary>
	/// <param name="car">Bug descriptor.</param>
	/// <returns>Pass result descriptor.</returns>
	public PassAttemptResult Pass(CarDesc car)
	{
		return mLogic.Pass(car);
	}
}