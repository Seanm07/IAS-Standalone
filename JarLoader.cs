// JarLoader.cs [Updated 10th August 2015]
// Attach this script to a persistent Game Object or use it from the plugins folder

/* Change Log:
 * 7th June 2015 (Sean)
 * - Initial file creation
 * 
 * 8th June 2015 (Jonni)
 * - Optional search for package manager
 * - Auto-instantiation
 * - Compiler directives
 * - GetPackageList returns a string
 * 
 * 8th June 2015 (Sean)
 * - Overall cleanup
 * - Fixed ActivityContext and JavaClass being re-set each time GetInstance() was called
 * - Static Instance replaced with a ScriptReady bool
 * - Debug.Log replaced with a DebugLog function allowing LoggingEnable to be toggled to set debug outputs
 * 
 * 11th June 2015 (Sean)
 * - Added the GetDensity function which interacts with the Java GetDensity function, returning an accurate screen DPI
 * - If the GetDensity request fails then it will fallback to Screen.dpi and a GoogleAnalytics error is logged
 * 
 * 4th August 2015(Sean)
 * - Added Haptic Feedback function
 * 
 * 10th August 2015 (Sean)
 * - Added function to get package name of this game (useful for reporting package name of stolen apps)
 * - Added GetAppInstallTimestamp() which returns a timestamp of when this app was installed OR updated (when the package installer last updated the app)
*/

using UnityEngine;
using System.Collections;

public class JarLoader : MonoBehaviour {

	private static bool LoggingEnabled = false; // Toggle debug outputs

	private static bool ScriptReady = false; // True once there is a gameobject in the scene with JarLoader.cs which has awaken
	private static bool ScriptInitialised = false;

	#if UNITY_ANDROID && !UNITY_EDITOR
	private static AndroidJavaObject ActivityContext;
	private static AndroidJavaClass JavaClass;
	#endif

	void Awake()
	{
		// Destroy if this already exists
		if(ScriptReady){
			Destroy(this);
			return;
		}

		ScriptReady = true;
	}

	private static void DebugLog(string Message)
	{
		if(LoggingEnabled)
			Debug.Log(Message);
	}

	private static void GetInstance()
	{
		DebugLog("Running GetInstance..");

		if(!ScriptInitialised){

			if(!ScriptReady){
				// JarLoader.cs wasn't attached to a persistant gameobject but a function was called!

				// Create a new gameobject for the JarLoader script
				GameObject JarLoaderObj = new GameObject("JarLoaderObj");

				// Attach the JarLoader script to the new gameobject and use this script for JarLoader.Instance references
				JarLoaderObj.AddComponent<JarLoader>();

				ScriptReady = true;

				DebugLog("JarLoader.cs was created");
			}

			#if UNITY_ANDROID && !UNITY_EDITOR
			if(ActivityContext == null || JavaClass == null){
				if(AndroidJNI.AttachCurrentThread() >= 0){
					ActivityContext = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
					JavaClass = new AndroidJavaClass("com.pickle.PackageLister.JarLoader");
				} else {
					DebugLog("Failed to attach current thread to Java (Dalvik) VM");
				}
			}

			ScriptInitialised = true;

			DebugLog("JarLoader.cs was initialised");

			#elif UNITY_EDITOR
				DebugLog("JarLoader.cs will not attach current thread in the editor!");
			#elif !UNITY_ANDROID
				DebugLog("JarLoader.cs will not attach current thread on non-android devices!");
			#endif
		}
	}

	/// <summary>
	/// Make the device vibrate a bit to give the user a response to their touch or to notify them
	/// </summary>
	/// <param name="Type">Haptic vibration type (1 = quick & weak, 2 = quick & mid, 3 = longer & strong)</param>
	public static void DoHapticFeedback(int Type = 1)
	{
		// Make sure we have an instance and the ActivityContext + Javaclass are ready
		GetInstance();

		DebugLog("Doing haptic feedback..");

		#if UNITY_ANDROID && !UNITY_EDITOR
			if(JavaClass != null && ActivityContext != null){
				JavaClass.CallStatic("HapticFeedback", ActivityContext, Type);
			} else {
				DebugLog("Failed to send haptic feedback!");
				GoogleAnalytics.Instance.LogError("Java Haptic Feedback failure!", false);
			}
		#elif UNITY_EDITOR 
			DebugLog ("JarLoader.cs will not send haptic feedback in the editor!");
		#elif !UNITY_ANDROID
			DebugLog("JarLoader.cs will not send haptic feedback on non-android devices!");
		#endif
	}

	public static void DoVibration(float Miliseconds, float Strength = 1f)
	{
		// Make sure we have an instance and the ActiviyContext + JavaClass are ready
		GetInstance();

		DebugLog("Doing vibration..");

		#if UNITY_ANDROID && !UNITY_EDITOR
			int IntStrength = Mathf.Clamp(Mathf.RoundToInt(Strength * 255f), 1, 255);
			long LongMiliseconds = (long)Miliseconds;

			if(JavaClass != null && ActivityContext != null){
		JavaClass.CallStatic("DoVibrate", ActivityContext, LongMiliseconds, IntStrength);
			} else {
				DebugLog("Failed to send vibration!");
				GoogleAnalytics.Instance.LogError("Java Vibration failure!", false);
			}
		#elif UNITY_EDITOR
			DebugLog("JarLoader.cs will not send vibration in the editor!");
		#elif !UNITY_ANDROID
			DebugLog("JarLoader.cs will not send vibration on non-android devices!");
		#endif
	}

	public static void StopVibration()
	{
		// Make sure we have any instance and the ActivityContext + JavaClass are ready
		GetInstance();

		Debug.Log("Stopping vibration..");

		#if UNITY_ANDROID && !UNITY_EDITOR
			if(JavaClass != null && ActivityContext != null){
				JavaClass.CallStatic("StopVibrate");
			} else {
				DebugLog("Failed to send stop vibration!");
				GoogleAnalytics.Instance.LogError("Java stop vibration failure!", false);
			}
		#elif UNITY_EDITOR
			DebugLog("JarLoader.cs will not send stop vibration in the editor!");
		#elif !UNITY_ANDROID
			DebugLog("JarLoader.cs will not send stoop vibration on non-android devices!");
		#endif
	}

	public static long GetAppInstallTimestamp()
	{
		// Make sure we have an instance and the ActivityContext + JavaClass is ready
		GetInstance();

		DebugLog("Getting app install timestamp..");

		#if UNITY_ANDROID && !UNITY_EDITOR
		if(JavaClass != null && ActivityContext != null){
		return JavaClass.CallStatic<long>("GetInstallTimestamp", ActivityContext);
		} else {
		DebugLog("Failed to get app install timestamp!");
		GoogleAnalytics.Instance.LogError("Java Timestamp failure! Cannot display pirate screen for this app clone!", false);
		}
		#elif UNITY_EDITOR
		DebugLog("JarLoader.cs will not get the install timestamp for this game in the editor!");
		#elif !UNITY_ANDROID
		DebugLog ("JarLoader.cs will not get the install timestamp for this game on non-android devices!");
		#endif

		// It failed so just return -1
		return -1L;
	}

	public static long GetMillisecondsSinceBoot()
	{
		// Make sure we have an instance and the ActivityContext + JavaClass is ready
		GetInstance();

		DebugLog("Getting milliseconds since boot..");

		#if UNITY_ANDROID && !UNITY_EDITOR
			if(JavaClass != null && ActivityContext != null){
				return JavaClass.CallStatic<long>("GetMillisecondsSinceBoot");
			} else {
				DebugLog("Failed to get milliseconds since boot!");
				GoogleAnalytics.Instance.LogError("Java milliseconds since boot failure!");
			}
		#elif UNITY_EDITOR
			DebugLog("JarLoaer.cs will not get the milliseconds since boot in the editor!");
		#elif !UNITY_ANDROID
			DebugLog("JarLoader.cs will not get the milliseconds since boot on non-android devices!");
		#endif

		// It failed to just return -1
		return -1L;
	}

	public static int GetDensity()
	{
		// Make sure we have an instance and the ActivityContext + JavaClass is ready
		GetInstance();

		DebugLog("Getting display density..");

		#if UNITY_ANDROID && !UNITY_EDITOR
		if(JavaClass != null && ActivityContext != null){
		return JavaClass.CallStatic<int>("GetDensity", ActivityContext);
		} else {
		DebugLog("Failed to get display density!");
		GoogleAnalytics.Instance.LogError("Java DPI failure! Falling back to unreliable Screen.dpi!", false);
		}
		#elif UNITY_EDITOR 
		DebugLog ("JarLoader.cs will not get display desity from jar in the editor!");
		#elif !UNITY_ANDROID
		DebugLog("JarLoader.cs will not get display desity on non-android devices!");
		#endif

		// Nothing has been returned yet so just return Screen.dpi instead (Note that this will return 0 if it fails)
		return Mathf.RoundToInt(Screen.dpi);
	}

	public static float GetXDPI()
	{
		// Make sure we have an instance and the ActivityContext + JavaClass is ready
		GetInstance();

		DebugLog("Getting XDPI..");

		#if UNITY_ANDROID && !UNITY_EDITOR
			if(JavaClass != null && ActivityContext != null){
				return JavaClass.CallStatic<float>("GetXDPI", ActivityContext);
			} else {
				DebugLog("Failed to get XDPI!");
				GoogleAnalytics.Instance.LogError("Java XDPI failure!", false);
			}
		#elif UNITY_EDITOR 
			DebugLog ("JarLoader.cs will not get XDPI from jar in the editor!");
		#elif !UNITY_ANDROID
			DebugLog("JarLoader.cs will not get XDPI on non-android devices!");
		#endif

		// Nothing has been returned yet so just return Screen.dpi instead (Note that this will return 0 if it fails)
		return Mathf.RoundToInt(Screen.dpi);
	}

	public static float GetYDPI()
	{
		// Make sure we have an instance and the ActivityContext + JavaClass is ready
		GetInstance();

		DebugLog("Getting YDPI..");

		#if UNITY_ANDROID && !UNITY_EDITOR
			if(JavaClass != null && ActivityContext != null){
				return JavaClass.CallStatic<float>("GetYDPI", ActivityContext);
			} else {
				DebugLog("Failed to get XDPI!");
				GoogleAnalytics.Instance.LogError("Java YDPI failure!", false);
			}
		#elif UNITY_EDITOR 
			DebugLog ("JarLoader.cs will not get YDPI from jar in the editor!");
		#elif !UNITY_ANDROID
			DebugLog("JarLoader.cs will not get YDPI on non-android devices!");
		#endif

		// Nothing has been returned yet so just return Screen.dpi instead (Note that this will return 0 if it fails)
		return Mathf.RoundToInt(Screen.dpi);
	}

	public static string GetSelfPackageName()
	{
		// Make sure we have an instance and the AcitivtyContext + JavaClass is ready
		GetInstance();

		DebugLog("Getting self package name..");

		string PackageName = "Unknown";

		#if UNITY_ANDROID && !UNITY_EDITOR
		if(JavaClass != null && ActivityContext != null){
		DebugLog("Getting self package name!");
		// Get the package name being used by this app on the current device
		PackageName = JavaClass.CallStatic<string>("GetSelfPackageName", ActivityContext);
		} else {
		DebugLog("The Java class or AcitivtyContext wasn't ready when getting package list!");
		}
		#endif

		return PackageName;
	}

	public static string GetPackageList(string searchString = default(string))
	{
		// Make sure we have an instance and the ActivityContext + JavaClass is ready
		GetInstance();

		DebugLog("Getting package list..");

		#if UNITY_ANDROID && !UNITY_EDITOR
		string PackageList = string.Empty;

		if(JavaClass != null && ActivityContext != null){
		DebugLog ("About to get package list, here we go!");
		// Get the list of installed packages on the device
		PackageList = JavaClass.CallStatic<string>("GetPackageList", ActivityContext, searchString);
		} else {
		DebugLog("The Java class or ActivityContext wasn't ready when getting package list!");
		}

		if (!string.IsNullOrEmpty (PackageList)) {
		DebugLog("Output: " + PackageList);
		} else {
		DebugLog("Output was null or empty!");
		}
		return PackageList;
		#elif UNITY_EDITOR
		DebugLog("JarLoader.cs will not GetPackageList in the editor!");
		#elif !UNITY_ANDROID
		DebugLog("JarLoader.cs will not GetPackageList on non-android devices!");
		#endif

		return string.Empty;
	}

	public static void DisplayToastMessage(string inString)
	{
		// Make sure we have an instance and the ActivityContext + JavaClass is ready
		GetInstance();

		#if UNITY_ANDROID && !UNITY_EDITOR
		if(JavaClass != null){
		// Get the list of installed packages on the device
		JavaClass.CallStatic("DisplayToast", ActivityContext, inString , 5);

		DebugLog("Toast has been popped!");
		} else {
		DebugLog("The Java class wasn't ready when displaying a toast!");
		}
		#else
		DebugLog("JarLoader.cs - Toast: " + inString);
		#endif
	}

	// MaxMemory - UsedMemory
	public static long GetAvailableMemory()
	{
		// Make sure we have an instance and the ActivityContext + JavaClass is ready
		GetInstance();

		#if UNITY_ANDROID && !UNITY_EDITOR
			if(JavaClass != null){
				return JavaClass.CallStatic<long>("GetAvailableMemory");
			} else {
				DebugLog("The Java class or AcitivtyContext wasn't ready when getting available memory!");
			}
		#endif

		// In the editor just get the total available system memory and convert it to bytes (from kb)
		return ((long)SystemInfo.systemMemorySize * 1000L);
	}

	// TotalMemory - FreeMemory
	public static long GetUsedMemory()
	{
		// Make sure we have an instance and the ActivityContext + JavaClass is ready
		GetInstance();

		#if UNITY_ANDROID && !UNITY_EDITOR
			if(JavaClass != null){
				return JavaClass.CallStatic<long>("GetUsedMemory");
			} else {
				DebugLog("The Java class or AcitivtyContext wasn't ready when getting used memory!");
			}
		#endif

		// In the editor get the used memory via System.GC
		return (System.GC.GetTotalMemory(false));
	}

	// Returns the total memory available to the Java VM (this value can change over time as the system assigns more memory into the Java VM)
	public static long GetTotalMemory()
	{
		// Make sure we have an instance and the ActivityContext + JavaClass is ready
		GetInstance();

		#if UNITY_ANDROID && !UNITY_EDITOR
			if(JavaClass != null){
				return JavaClass.CallStatic<long>("GetTotalMemory");
			} else {
				DebugLog("The Java class or AcitivtyContext wasn't ready when getting total memory!");
			}
		#endif

		// In the editor just get the total available system memory and convert it to bytes (from kb)
		return ((long)SystemInfo.systemMemorySize * 1000L);
	}

	// Returns the maximum amount of memory that the Java VM will attempt to use
	public static long GetMaxMemory()
	{
		// Make sure we have an instance and the ActivityContext + JavaClass is ready
		GetInstance();

		#if UNITY_ANDROID && !UNITY_EDITOR
			if(JavaClass != null){
				return JavaClass.CallStatic<long>("GetMaxMemory");
			} else {
				DebugLog("The Java class or AcitivtyContext wasn't ready when getting max memory!");
			}
		#endif

		// In the editor just get the total available system memory and convert it to bytes (from kb)
		return ((long)SystemInfo.systemMemorySize * 1000L);
	}

	// Amount of free memory currently allocated to the Java VM (more may be assigned by the system as needed)
	public static long GetFreeMemory()
	{
		// Make sure we have an instance and the ActivityContext + JavaClass is ready
		GetInstance();

		#if UNITY_ANDROID && !UNITY_EDITOR
			if(JavaClass != null){
				return JavaClass.CallStatic<long>("GetFreeMemory");
			} else {
				DebugLog("The Java class or AcitivtyContext wasn't ready when getting free memory!");
			}
		#endif

		// In the editor..
		return (System.GC.GetTotalMemory(false) - ((long)SystemInfo.systemMemorySize * 1000L));
	}

	public static void CancelToastMessage()
	{
		// Make sure we have an instance and the ActivityContext + JavaClass is ready
		GetInstance();

		#if UNITY_ANDROID && !UNITY_EDITOR
		if(JavaClass != null){
		// Get the list of installed packages on the device
		JavaClass.CallStatic("ForceEndToast");

		DebugLog("Toast has been cancelled!");
		} else {
		DebugLog("The Java class wasn't ready when cancelling a toast!");
		}
		#else
		DebugLog("JarLoader.cs - Cancelling Toast");
		#endif
	}

	// Returns an array of accounts on the device in hash form
	// Useful for comparing player identities for IAB and restoring purchases
	/*public static string[] GetAccountHashes()
	{
		// Make sure we have an instance and the ActivityContext + JavaClass is ready
		GetInstance();

		#if UNITY_ANDROID && !UNITY_EDITOR
			if(JavaClass != null){
				// Get an array of accounts on the device
				JavaClass.CallStatic<string[]>("GetAccounts", ActivityContext);

				DebugLog("Accounts have been requested!");
			} else {
				DebugLog("The java class wasn't ready when requesting accounts!");
			}
		#else
			DebugLog("JarLoader.cs Requested account hashes!");
		#endif

		return new string[0];
	}*/

}