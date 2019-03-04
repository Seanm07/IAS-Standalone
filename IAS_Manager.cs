using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using System.Text.RegularExpressions;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;

#if !UNITY_5_3_OR_NEWER
	// Older versions of Unity don't have the built in JSONUtility so SimpleJSON is required
	using SimpleJSON;
#endif

#if UNITY_EDITOR
	using UnityEditor;
#endif

// Storage classes for IAS adverts
[Serializable]
public class AdJsonFileData {
	#if UNITY_EDITOR
		public string name;
	#endif

	public List<AdSlotData> slotInts = new List<AdSlotData>();

	public AdJsonFileData(List<AdSlotData> newSlotIntsData = null)
	{
		slotInts = (newSlotIntsData == null ? new List<AdSlotData>() : newSlotIntsData);
	}
}

[Serializable]
public class AdSlotData {
	#if UNITY_EDITOR
		public string name;
	#endif

	public int slotInt; // Number from the slotID

	public List<AdData> advert = new List<AdData>();

	public int lastSlotId;

	public AdSlotData(int newSlotInt = -1, List<AdData> newAdvert = null)
	{
		slotInt = newSlotInt;
		advert = (newAdvert == null ? new List<AdData>() : newAdvert);
	} 
}

[Serializable]
public class AdData {
	#if UNITY_EDITOR
		public string name;
	#endif

	public char slotChar; // Character from the slotID
	public string fileName; // Name this ad file will be named as on the device

	public bool isTextureFileCached; // Has the texture been saved to the device
	public bool isTextureReady; // Has the texture finished downloading
	public bool isInstalled; // Is this an advert for an app which is already installed?
	public bool isSelf; // Is this an advert for this game?
	public bool isActive; // Is this an advert marked as active in the json file?
	public bool isDownloading;

	public long lastUpdated; // Timestamp of when the ad was last updated
	public long newUpdateTime; // Timestamp of the newly collected ad data

	public string imgUrl; // URL of the image we need to download
	public string adUrl; // URL the player is taken to when clicking the ad
	public string packageName;

	public int adTextureId = -1; // Reference id to which the texture for this ad is stored in

	public AdData(char inSlotChar = default(char))
	{
		slotChar = inSlotChar;
	}
}

// These classes are for the JsonUtility to move the data into after they've been ready from the file
[Serializable]
public class JsonFileData {
	public List<JsonSlotData> slots;
	public List<JsonSlotData> containers;
}

[Serializable]
public class JsonSlotData {
	public string slotid;

	public long updatetime;
	public bool active;

	public string adurl;
	public string imgurl;
}

[Serializable]
public class AdOffsets {
	public int slotid;
	public int maxPreloadedAdOffset;

	public AdOffsets (int inSlotId, int inMaxPreloadedAdOffset)
	{
		slotid = inSlotId;
		maxPreloadedAdOffset = inMaxPreloadedAdOffset;
	}
}

public class IAS_Manager : MonoBehaviour
{
	public static IAS_Manager Instance;

	#if (!UNITY_5_3_OR_NEWER) || !UNITY_ANDROID
		public string bundleId = "com.example.GameNameHere";
		public string appVersion = "1.00";
	#else
		public string bundleId { get; private set; }
		public string appVersion { get; private set; }
	#endif

	private int internalScriptVersion = 24;

	public enum Platform { Standard, TV }
	public Platform platform = Platform.Standard;

	// JSON URLs where the ads are grabbed from
	#if UNITY_IOS
		public string[] jsonUrls = new string[1]{"https://ias.gamepicklestudios.com/ad/3.json"};
	#else
		public string[] jsonUrls = new string[1]{"https://ias.gamepicklestudios.com/ad/1.json"}; // https://ads2.gumdropgames.com/ad/4.json
	#endif

	private int slotIdDecimalOffset = 97; // Decimal offset used to start our ASCII character at 'a'

	// List of apps installed on the player device matching our filter
	private List<string> installedApps = new List<string>();

	public bool useStorageCache = true; // Should ads be downloaded to the device for use across sessions
	public bool advancedLogging = false; // Enable this to debug the IAS with more debug logs

	public List<AdOffsets> maxOffsetAds = new List<AdOffsets>(new AdOffsets[]{ new AdOffsets(1, 3), new AdOffsets(2, 0) }); // This will keep x ads after the active ad preloaded (useful for backscreen preloading with a value of 3)

	// Private to hide from developers as we never want to disable these
	private bool logAdImpressions = true; // DO NOT DISABLE! This will affect our stats, instead talk to use about your issue
	private bool logAdClicks = true; // DO NOT DISABLE! This will affect our stats, instead talk to use about your issue

	public List<int> blacklistedSlots = new List<int>();

	public static Action OnIASImageDownloaded;
	public static Action OnForceChangeWanted;

	// Optimization to group together save calls (also delays saving so they won't happen as soon as the app is launched)
	private int framesUntilIASSave = -1;

	// Variable to make sure the force save at app quit isn't called multiple times
	// (is also set back to false if the user comes back to the app from being minimized)
	private bool hasQuitForceSaveBeenCalled = false;

	#if UNITY_EDITOR
		[ContextMenu("Open IAS GitHub URL")]
		private void OpenIASGithub()
		{
			Application.OpenURL("https://github.com/Seanm07/IAS-Standalone/");
		}

		// When true the script checks for a new version when entering play mode
		public bool checkForLatestVersion = true;
	
		private IEnumerator CheckIASVersion()
		{
			WWW versionCheck = new WWW("https://data.i6.com/IAS/ias_check.txt");

			yield return versionCheck;

			int latestVersion = 0;

			int.TryParse(versionCheck.text, out latestVersion);

			if(latestVersion > internalScriptVersion){
				if(EditorUtility.DisplayDialog("IAS Update Available!", "There's a new version of the IAS script available!\nWould you like to update now?\n\nIAS files will be automatically replaced with their latest versions!", "Yes", "No")){
					string scriptPath = EditorUtility.OpenFilePanel("Select IAS_Manager.cs from your project!", "", "cs");

					if(scriptPath.Length > 0){
						// Remove assets from the path because Unity 5.4.x has a bug where the return value of the path doesn't include assets unlike other versions of unity
						scriptPath = scriptPath.Replace("Assets/", "");

						// Re-add Assets/ but also remove the data path so the path starts at Assets/
						scriptPath = scriptPath.Replace(Application.dataPath.Replace("Assets", ""), "Assets/");

						WWW scriptDownload = new WWW("https://data.i6.com/IAS/GamePickle/IAS_Manager.cs");

						yield return scriptDownload;

						FileStream tmpFile = File.Create(scriptPath + ".tmp");
						FileStream backupFile = File.Create(scriptPath + ".backup" + internalScriptVersion);

						tmpFile.Close();
						backupFile.Close();

						File.WriteAllText(scriptPath + ".tmp", scriptDownload.text);
						File.Replace(scriptPath + ".tmp", scriptPath, scriptPath + ".backup");
						File.Delete(scriptPath + ".tmp");

						// Update the AssetDatabase so we can see the file changes in Unity
						AssetDatabase.Refresh();

						Debug.Log("IAS upgraded from version " + internalScriptVersion + " to " + latestVersion);

						// Force exit play mode
						EditorApplication.isPlaying = false;
					} else {
						Debug.LogError("Update cancelled! Did not select the IAS_Manager.cs script!");
					}
				} else {
					Debug.LogError("Update cancelled! Make sure to update your IAS version before sending a build!");
				}
			}
		}
	#endif

	[Header("Exposed for monitoring, leave below empty!")]
	// Contains information about the adverts we have available to be displayed and their statuses
	public List<AdJsonFileData> advertData = new List<AdJsonFileData>();

	// The textures are in a separate list so we can serialize the advertData to save it across sessions
	public List<Texture> advertTextures = new List<Texture>();

	#if UNITY_ANDROID
		private void UpdateInstalledPackages()
		{
			if(advancedLogging)
				Debug.Log("IAS Updating Installed Packages");

			installedApps.Clear();

			// Get all installed packages with a bundleId matching our filter
			string filteredPackageListPickle = JarLoader.GetPackageList("com.pickle.");
			string filteredPackageListGumdrop = JarLoader.GetPackageList("com.gumdropgames.");
			//string filteredPackageList = "com.pickle.StreetRacingCarDriver, com.pickle.PoliceMotorbikeSimulator3D, com.pickle.HelicopterFlyingRescueSimulator, com.pickle.PoliceCarDrivingOffroad, com.pickle.police_motorbike_driving_simulator, com.pickle.OffroadDrivingSim6x6, com.pickle.CityDriver, com.pickle.Construction2017"; 

			int installedPickleCount = 0; 
			int installedGumdropGames = 0;

			// Added a length check because I can't remember if there's just a comma or 2 spaces and a comma if the list is empty
			string filteredPackageList = (filteredPackageListPickle.Length >= 3 ? filteredPackageListPickle : "") + filteredPackageListGumdrop;

			// Cleanup the package list mistakes (ending comma or any spaces)
			if(!string.IsNullOrEmpty(filteredPackageList)){
				filteredPackageList = filteredPackageList.Trim(); // Trim whitespaces

				if(filteredPackageList.Length > 0){
					filteredPackageList = filteredPackageList.Remove(filteredPackageList.Length - 1); // Remove the unwanted comma at the end of the list
				}

				filteredPackageListPickle = filteredPackageListPickle.Trim();

				if(filteredPackageListPickle.Length > 0){
					filteredPackageListPickle = filteredPackageListPickle.Remove(filteredPackageListPickle.Length - 1);
					installedPickleCount = filteredPackageListPickle.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Length;
				}

				filteredPackageListGumdrop = filteredPackageListGumdrop.Trim();

				if(filteredPackageListGumdrop.Length > 0){
					filteredPackageListGumdrop = filteredPackageListGumdrop.Remove(filteredPackageListGumdrop.Length - 1);
					installedGumdropGames = filteredPackageListGumdrop.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Length;
				}

				// Split the list into a string array
				string[] packageArray = filteredPackageList.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

				if(packageArray.Length > 0){
					// Extract all packages and store them in the installedApps list
					foreach(string packageName in packageArray){
						installedApps.Add(packageName.Trim().ToLowerInvariant());
					}
				} else {
					if(advancedLogging)
						Debug.Log("No other installed packages found matching filter!");
				}
			} else {
				if(advancedLogging)
					Debug.Log("Filtered package list was empty!");
			}

			if (!PlayerPrefs.HasKey ("IASTotalGamesLogged")) {
				GoogleAnalytics.Instance.IASLogEvent("Total GamePickle games installed", installedPickleCount.ToString());
				GoogleAnalytics.Instance.IASLogEvent("Total GumDrop games installed", installedGumdropGames.ToString());
				PlayerPrefs.SetInt ("IASTotalGamesLogged", 1);
			}
		}
	#endif

	void Awake()
	{
		// Destroy if this already exists
		if(Instance){
			Destroy(this);
			return;
		}

		Instance = Instance ?? this;

		if(platform == Platform.TV)
			jsonUrls[0] = "https://ias.gamepicklestudios.com/ad/6.json"; // https://ads2.gumdropgames.com/ad/8.json

		#if (UNITY_5 || UNITY_2017 || UNITY_2018) && UNITY_ANDROID
			bundleId = Application.identifier;
			appVersion = Application.version;
		#else
			if(bundleId == "com.example.GameNameHere")
				GoogleAnalytics.Instance.IASLogError("IAS bundle identifier not set in an APK build!", false);
		#endif

		#if UNITY_EDITOR
			if(checkForLatestVersion)
				StartCoroutine(CheckIASVersion());
		#endif
	}

	void Start()
	{
		// If the line below is giving you an error then you also need to update your GoogleAnalytics.cs, download the latest here: https://data.i6.com/IAS/GoogleAnalytics.cs
		if(GoogleAnalytics.Instance.IASPropertyID != "UA-120537332-1"){
			Debug.LogError("Invalid IAS property ID! Make sure the IAS Property ID is set to UA-120537332-1 in the inspector of GoogleAnalytics.cs!");

			#if UNITY_EDITOR
			EditorUtility.DisplayDialog("Invalid IAS property ID!", "Make sure the IAS Property ID is set to UA-120537332-1 in the inspector of GoogleAnalytics.cs!", "OK (Message also appears in console)");
			#endif
		}

		Debug.Log("IAS Init [" + internalScriptVersion + "] " + bundleId  + " (" + appVersion + ") - IASLog[" + (GoogleAnalytics.Instance.IASPropertyID == "UA-120537332-1" ? "PASS" : "FAIL") + "] ImpLog[" + (logAdImpressions ? "PASS" : "FAIL") + "] ClkLog[" + (logAdClicks ? "PASS" : "FAIL") + "]");

		#if UNITY_ANDROID
			// Get a list of installed packages on the device and store ones matching a filter
			UpdateInstalledPackages();
		#endif

		bool cachedIASDataLoaded = LoadIASData();

		StartCoroutine(DownloadIASData(cachedIASDataLoaded));

		// If there was some cached IAS data available refresh the ads now
		// The ads will also be refreshed once the IAS data reloads if any ad timestamps have changed
		if(cachedIASDataLoaded)
			RefreshActiveAdSlots();
	}

	void Update()
	{
		if(framesUntilIASSave > 0){
			framesUntilIASSave--;

			if(framesUntilIASSave == 0)
				SaveIASData(true);
		}

		/*#if UNITY_EDITOR
			if(Input.GetKeyDown(KeyCode.R)){
				IAS_Manager.RefreshBanners(0, 1, true);
				IAS_Manager.RefreshBanners(0, 2, true);
			}
		#endif*/
	}
	

	private void RefreshActiveAdSlots()
	{
		// Refresh an ad for each slot int so they all have an active ad loaded and ready to be displayed
		for(int jsonFileId=0;DoesSlotFileIdExist(jsonFileId);jsonFileId++)
			for(int i=1;DoesSlotIntExist(jsonFileId, i);i++)
				RefreshBanners(jsonFileId, i);
	}

	private void RefreshActiveAdSlots(int jsonFileId, List<AdJsonFileData> customData = null)
	{
		for(int i=1;DoesSlotIntExist(jsonFileId, i, customData);i++)
			RefreshBanners(jsonFileId, i, false, customData);
	}

	private void RandomizeAdSlots(int jsonFileId, List<AdJsonFileData> customData = null)
	{
		for(int i=1;DoesSlotIntExist(jsonFileId, i);i++)
		{
			AdSlotData curSlotData = GetAdSlotData(jsonFileId, i, customData);

			curSlotData.lastSlotId = UnityEngine.Random.Range(0, curSlotData.advert.Count-1);
		}
	}

	// Used when we need to modify the values of AdJsonFileData temporarily without changing source values
	private List<AdJsonFileData> DeepCopyAsJsonFileData(List<AdJsonFileData> input)
	{
		List<AdJsonFileData> output = new List<AdJsonFileData>();

		for(int jsonFileId=0;jsonFileId < input.Count;jsonFileId++)
		{
			AdJsonFileData curInputJsonFile = input[jsonFileId];

			output.Add(new AdJsonFileData());

			AdJsonFileData curOutputJsonFile = output[jsonFileId];

			#if UNITY_EDITOR
				curOutputJsonFile.name = ConvertToSecureProtocol(jsonUrls[jsonFileId]);
			#endif

			// Slot IDs determine ad sizes and are the numbers in the slots 1a, 1b, 2a etc
			for(int slotId=0;slotId < curInputJsonFile.slotInts.Count;slotId++)
			{
				AdSlotData curInputSlot = curInputJsonFile.slotInts[slotId];

				curOutputJsonFile.slotInts.Add(new AdSlotData());

				AdSlotData curOutputSlot = curOutputJsonFile.slotInts[slotId];

				#if UNITY_EDITOR
					curOutputSlot.name = "Slot " + curInputJsonFile.slotInts[slotId].slotInt;
				#endif

				curOutputSlot.slotInt = curInputSlot.slotInt;
				curOutputSlot.lastSlotId = curInputSlot.lastSlotId;

				// Ad IDs determine the ads within the slots of sizes, they're the characters in the slots 1a, 1b, 1a etc
				for(int adId=0;adId < curInputSlot.advert.Count;adId++)
				{
					AdData curInputAdvert = curInputSlot.advert[adId];

					curOutputSlot.advert.Add(new AdData());

					AdData curOutputAdvert = curOutputSlot.advert[adId];

					#if UNITY_EDITOR
						curOutputAdvert.name = curInputJsonFile.slotInts[slotId].slotInt + curInputAdvert.slotChar.ToString();
					#endif

					curOutputAdvert.slotChar = curInputAdvert.slotChar;
					curOutputAdvert.fileName = curInputAdvert.fileName;
					curOutputAdvert.isTextureFileCached = curInputAdvert.isTextureFileCached;
					curOutputAdvert.isTextureReady = curInputAdvert.isTextureReady;
					curOutputAdvert.isInstalled = curInputAdvert.isInstalled;
					curOutputAdvert.isSelf = curInputAdvert.isSelf;
					curOutputAdvert.isActive = curInputAdvert.isActive;
					curOutputAdvert.isDownloading = curInputAdvert.isDownloading;
					curOutputAdvert.lastUpdated = curInputAdvert.lastUpdated;
					curOutputAdvert.newUpdateTime = curInputAdvert.newUpdateTime;
					curOutputAdvert.imgUrl = ConvertToSecureProtocol(curInputAdvert.imgUrl);
					curOutputAdvert.adUrl = ConvertToSecureProtocol(curInputAdvert.adUrl);
					curOutputAdvert.packageName = curInputAdvert.packageName;
					curOutputAdvert.adTextureId = curInputAdvert.adTextureId;
				}
			}
		}

		return output;
	}

	private string ConvertToSecureProtocol(string inputURL)
	{
		// C# internally checks the replace with indexOf anyway so no need to wrap with contains
		inputURL = inputURL.Replace("http://", "https://");

		return inputURL;
	}

	private string EncodeIASData()
	{
		try {
			List<AdJsonFileData> saveReadyAdvertData = DeepCopyAsJsonFileData(advertData);

			// Some parts of the data needs their values changing as they won't be valid for future sessions
			foreach(AdJsonFileData curFileData in saveReadyAdvertData)
			{
				foreach(AdSlotData curSlotData in curFileData.slotInts)
				{
					foreach(AdData curData in curSlotData.advert)
					{
						curData.adTextureId = -1;
						curData.isTextureReady = false;
						curData.isDownloading = false;
						curData.lastUpdated = 0L;
					}
				}
			}

			BinaryFormatter binaryData = new BinaryFormatter();
			MemoryStream memoryStream = new MemoryStream();

			// Serialize our data list into the memory stream
			binaryData.Serialize(memoryStream, (object)saveReadyAdvertData);

			string base64Data = string.Empty;

			try {
				// Convert the buffer of the memory stream (the serialized object) into a base 64 string
				base64Data = Convert.ToBase64String(memoryStream.GetBuffer());
			} catch(FormatException e){
				GoogleAnalytics.Instance.IASLogError("Corrupt mStream! " + e.Message, false);
				throw;
			}

			return base64Data;
		} catch(SerializationException e){
			GoogleAnalytics.Instance.IASLogError("Encode IAS Fail! " + e.Message, false);
			throw;
		}
	}

	private List<AdJsonFileData> DecodeIASData(string rawBase64Data)
	{
		try {
			BinaryFormatter binaryData = new BinaryFormatter();
			MemoryStream memoryStream = new MemoryStream(Convert.FromBase64String(rawBase64Data));

			try {
				return (List<AdJsonFileData>)binaryData.Deserialize(memoryStream);
			} catch(SerializationException e){
				GoogleAnalytics.Instance.IASLogError("Decode Fail! " + e.Message, false);
				throw;
			}
		} catch(FormatException e){
			GoogleAnalytics.Instance.IASLogError("Corrupt IAS Data! " + e.Message);
			throw;
		}
	}

	private void SaveIASData(bool forceSave = false)
	{
		if(!forceSave){
			// If the save function is called multiple times within 15 frames it'll just reset the timer before the save happens
			// Unless forceSave is true which either means the user to quitting the app or framesUntilIASSave is 0
			framesUntilIASSave = 15;
			return;
		} else {
			framesUntilIASSave = -1;
		}

		// Make sure the advertData has actually been setup before trying to save it
		if(advertData != null){
			string iasData = EncodeIASData();

			PlayerPrefs.SetString("IASAdvertData", iasData);
		}
	}

	private bool LoadIASData()
	{
		string loadedIASData = PlayerPrefs.GetString("IASAdvertData", string.Empty);

		if(!string.IsNullOrEmpty(loadedIASData)){
			advertData = DecodeIASData(loadedIASData);

			if(advertData != null)
				return true;
		}

		return false;
	}

	private bool IsPackageInstalled(string packageName)
	{
		foreach(string comparisonApp in installedApps)
			if(packageName.ToLowerInvariant().Contains(comparisonApp))
				return true;

		return false;
	}

	private bool DoesSlotFileIdExist(int jsonFileId, List<AdJsonFileData> customData = null)
	{
		return ((jsonFileId >= (customData != null ? customData.Count : advertData.Count)) ? false : true);
	}

	private bool DoesSlotIntExist(int jsonFileId, int wantedSlotInt, List<AdJsonFileData> customData = null)
	{
		return ((GetAdSlotData(jsonFileId, wantedSlotInt, customData) == null) ? false : true);
	}

	private bool DoesSlotCharExist(int jsonFileId, int wantedSlotInt, char wantedSlotChar, List<AdJsonFileData> customData = null)
	{
		return ((GetAdDataByChar(jsonFileId, wantedSlotInt, wantedSlotChar, customData) == null) ? false : true);
	}

	private int GetSlotIndex(int jsonFileId, int wantedSlotInt, List<AdJsonFileData> customData = null)
	{
		if(DoesSlotFileIdExist(jsonFileId, customData) && (customData != null ? customData[jsonFileId].slotInts != null : advertData[jsonFileId].slotInts != null)){
			// Iterate through each slot in the requested json file
			for(int i=0;i < (customData != null ? customData[jsonFileId].slotInts.Count : advertData[jsonFileId].slotInts.Count);i++)
			{
				AdSlotData curSlotData = (customData != null ? customData[jsonFileId].slotInts[i] : advertData[jsonFileId].slotInts[i]);

				// Check if this ad slot int matched the one we requested
				if(curSlotData.slotInt == wantedSlotInt)
					return i;
			}
		}

		return -1;
	}

	private int GetAdIndex(int jsonFileId, int wantedSlotInt, char wantedSlotChar, List<AdJsonFileData> customData = null)
	{
		AdSlotData slotData = GetAdSlotData(jsonFileId, wantedSlotInt, customData);

		if(slotData.advert != null){
			for(int i=0;i < slotData.advert.Count;i++)
			{
				AdData curAdData = slotData.advert[i];

				if(wantedSlotChar == curAdData.slotChar)
					return i;
			}
		}

		return -1;
	}

	private AdSlotData GetAdSlotData(int jsonFileId, int wantedSlotInt, List<AdJsonFileData> customData = null)
	{
		if(DoesSlotFileIdExist(jsonFileId, customData) && (customData != null ? customData[jsonFileId].slotInts != null : advertData[jsonFileId].slotInts != null)){
			// Iterate through each slot in the requested json file
			foreach(AdSlotData curSlotData in (customData != null ? customData[jsonFileId].slotInts : advertData[jsonFileId].slotInts))
			{
				// Check if this ad slot int matches the one we requested
				if(curSlotData.slotInt == wantedSlotInt)
					return curSlotData;
			}
		}

		return null;
	}

	private AdData GetAdData(int jsonFileId, int wantedSlotInt, int offset, List<AdJsonFileData> customData = null)
	{
		return GetAdDataByChar(jsonFileId, wantedSlotInt, GetSlotChar(jsonFileId, wantedSlotInt, offset, customData), customData);
	}

	// This was originally just an override of the above function but in Unity 4 char is treated as an int which confuses Unity on which function to use..
	private AdData GetAdDataByChar(int jsonFileId, int wantedSlotInt, char wantedSlotChar, List<AdJsonFileData> customData = null)
	{
		AdSlotData curAdSlotData = GetAdSlotData(jsonFileId, wantedSlotInt, customData);

		if(curAdSlotData != null){
			foreach(AdData curData in curAdSlotData.advert)
			{
				// Check if this ad slot character matches the one we requested
				if(curData.slotChar == wantedSlotChar)
					return curData;
			}
		}

		return null;
	}

	private void IncSlotChar(int jsonFileId, int wantedSlotInt, List<AdJsonFileData> customData = null)
	{
		// Exit early if the wanted slot is blacklisted
		foreach(int blacklistedSlot in blacklistedSlots)
			if(wantedSlotInt == blacklistedSlot) return;
		
		AdSlotData wantedSlotData = GetAdSlotData(jsonFileId, wantedSlotInt, customData);

		if(customData == null){
			// Calculate the next valid slot char to be displayed
			char wantedSlotChar = GetSlotChar(jsonFileId, wantedSlotInt, 0, customData);

			wantedSlotData.lastSlotId = (((int)wantedSlotChar) - slotIdDecimalOffset);

			if(advancedLogging)
				Debug.Log("Last char was " + wantedSlotChar);
		}

		StartCoroutine(DownloadAdTexture(jsonFileId, wantedSlotInt));
	}

	private int GetMaxAdOffset(int wantedSlotId)
	{
		foreach(AdOffsets curOffset in maxOffsetAds)
			if(curOffset.slotid == wantedSlotId)
				return curOffset.maxPreloadedAdOffset;

		return 0;
	}

	private IEnumerator DownloadAdTexture(int jsonFileId, int wantedSlotInt)
	{
		// Wait a frame just so calls to load textures aren't running instantly at app launch
		yield return null;

		int maxAdOffset = GetMaxAdOffset(wantedSlotInt);

		// We only need to preload ads for slotInt 1 which is the square ads for the backscreen
		for(int i=0;i < maxAdOffset+1 && (i < advertData[jsonFileId].slotInts[wantedSlotInt-1].advert.Count);i++)
		{
			char slotChar = GetSlotChar(jsonFileId, wantedSlotInt, i);
			AdData curAdData = GetAdDataByChar(jsonFileId, wantedSlotInt, slotChar);

			if(advancedLogging){
				Debug.Log("char was " + slotChar);
				Debug.Log("i was " + i);

				Debug.Log("(char: " + slotChar + ") Load tex for " + curAdData.adUrl);

			}

			if(curAdData != null && !curAdData.isDownloading){
				// Download the texture for the newly selected IAS advert
				// Only bother re-downloading the image if the timestamp has changed or the texture isn't marked as ready
				if(!curAdData.isTextureReady || curAdData.lastUpdated < curAdData.newUpdateTime){
					// Check if this is an advert we may be using in this game
					// Note: We still download installed ads because we might need them if there's no ads to display
					if(!curAdData.isSelf && curAdData.isActive){
						// Whilst we still have wwwImage write the bytes to disk to save on needing extra operations
						string filePath = Application.persistentDataPath + Path.AltDirectorySeparatorChar; 

						string fileName = "IAS_" + curAdData.fileName;

						// Set this info before downloading
						curAdData.lastUpdated = curAdData.newUpdateTime;
						curAdData.isDownloading = true;

						// Check to see if we have this advert locally cached
						if(curAdData.isTextureFileCached){
							//if(advancedLogging)
							//	Debug.Log("Starting ad download of " + curAdData.packageName + " " + wantedSlotInt + "" + slotChar + " (CACHED)");

							// Make sure the cache file actually exists (unexpected write fails or manual deletion)
							if(File.Exists(filePath + fileName)){
								try {
									// Read the saved texture from disk
									byte[] imageData = File.ReadAllBytes(filePath + fileName);

									TextureFormat imageTextureFormat;

									// Detect system compatbility for texture compression formats and use the most efficient
									// Several IAS adverts will be in memory at once so compression is important
									// When a compression format isn't supported the system falls into software decompression mode which means using the textures will be very heavy on performance
									if(SystemInfo.SupportsTextureFormat(TextureFormat.PVRTC_RGBA2)){
										// Smallest, fastest compression option but does not support ALL android GPUs
										// However on iOS it should have full support, whereas on the otherhand iOS doesn't seem to support OpenGLES 2
										imageTextureFormat = TextureFormat.PVRTC_RGBA2;
									} else if(SystemInfo.SupportsTextureFormat(TextureFormat.ETC2_RGBA1)){
										// Smallest fastest ETC2 format which supports alpha
										imageTextureFormat = TextureFormat.ETC2_RGBA1;
									} else if(SystemInfo.SupportsTextureFormat(TextureFormat.ETC2_RGBA8)){
										// Last alternative for ETC2 with alpha support
										imageTextureFormat = TextureFormat.ETC2_RGBA8;
									} else if(SystemInfo.SupportsTextureFormat(TextureFormat.RGBA32)){
										// Other compression formats don't seem to be supported, atleast RGBA32 should be.. right?!?
										imageTextureFormat = TextureFormat.RGBA32;
									} else {
										// RGBA32 shouldALWAYS be support so we should never be here unless a crazy device with literally no alpha support exists..
										// However if a crazy no alpha support device exists then fallback to RGB24
										imageTextureFormat = TextureFormat.RGB24;
									}

									// We need to create a template texture, we're also setting the compression type here
									Texture2D imageTexture = new Texture2D(2, 2, imageTextureFormat, false);

									#if UNITY_EDITOR
										imageTexture.name = wantedSlotInt + slotChar.ToString() + " - " + ConvertToSecureProtocol(jsonUrls[jsonFileId]);
									#endif

									// Load the image data, this will also resize the texture
									imageTexture.LoadImage(imageData);

									// Note! Compression doesn't work here because we can't resize an image (easily) or change the texture format (easily)
									// It requires performance expensive operations

									advertTextures.Add(imageTexture);
								} catch(IOException e){
									GoogleAnalytics.Instance.IASLogError("Failed to load cached file! " + e.Message);
									curAdData.isTextureFileCached = false;

									SaveIASData();

									yield break;
								}
							} else {
								GoogleAnalytics.Instance.IASLogError("Saved cached image was missing!");
								curAdData.isTextureFileCached = false;

								SaveIASData();

								// Retry the download now that we know the cached image is missing
								StartCoroutine(DownloadAdTexture(jsonFileId, wantedSlotInt));

								yield break;
							}
						} else {
							//if(advancedLogging)
							//	Debug.Log("Starting ad download of " + curAdData.packageName + " " + wantedSlotInt + "" + slotChar + " (NOT CACHED)");

							// The advert is not yet locally cached, 
							WWW wwwImage = new WWW(curAdData.imgUrl);

							// Wait for the image data to be downloaded
							yield return wwwImage;

							// Need to re-grab curAdData just incase it has been overwritten
							curAdData = GetAdDataByChar(jsonFileId, wantedSlotInt, slotChar);

							// Check for any errors
							if(!string.IsNullOrEmpty(wwwImage.error)){
								GoogleAnalytics.Instance.IASLogError("Image download error! " + wwwImage.error);
								yield break;
							} else if(wwwImage.text.Contains("There was an error")){
								GoogleAnalytics.Instance.IASLogError("Image download error! Serverside system error!");
								yield break;
							} else if(string.IsNullOrEmpty(wwwImage.text)){
								GoogleAnalytics.Instance.IASLogError("Image download error! Empty result!");
								yield break;
							}

							TextureFormat imageTextureFormat;

							// Detect system compatbility for texture compression formats and use the most efficient
							// Several IAS adverts will be in memory at once so compression is important
							// When a compression format isn't supported the system falls into software decompression mode which means using the textures will be very heavy on performance
							if(SystemInfo.SupportsTextureFormat(TextureFormat.PVRTC_RGBA2)){
								// Smallest, fastest compression option but does not support ALL android GPUs
								// However on iOS it should have full support, whereas on the otherhand iOS doesn't seem to support OpenGLES 2
								imageTextureFormat = TextureFormat.PVRTC_RGBA2;
							} else if(SystemInfo.SupportsTextureFormat(TextureFormat.ETC2_RGBA1)){
								// Smallest fastest ETC2 format which supports alpha
								imageTextureFormat = TextureFormat.ETC2_RGBA1;
							} else if(SystemInfo.SupportsTextureFormat(TextureFormat.ETC2_RGBA8)){
								// Last alternative for ETC2 with alpha support
								imageTextureFormat = TextureFormat.ETC2_RGBA8;
							} else if(SystemInfo.SupportsTextureFormat(TextureFormat.RGBA32)){
								// Other compression formats don't seem to be supported, atleast RGBA32 should be.. right?!?
								imageTextureFormat = TextureFormat.RGBA32;
							} else {
								// RGBA32 shouldALWAYS be support so we should never be here unless a crazy device with literally no alpha support exists..
								// However if a crazy no alpha support device exists then fallback to RGB24
								imageTextureFormat = TextureFormat.RGB24;
							}

							// Create a template texture for the downloaded image to be loaded into, this lets us set the compression type and disable mipmaps (we disable mipmaps so the texture quality setting doesn't affect IAS ads) 
							Texture2D imageTexture = new Texture2D(2, 2, imageTextureFormat, false);

							#if UNITY_EDITOR
								imageTexture.name = wantedSlotInt + slotChar.ToString() + " - " + ConvertToSecureProtocol(jsonUrls[jsonFileId]);
							#endif

							wwwImage.LoadImageIntoTexture(imageTexture);

							advertTextures.Add(imageTexture);

							try {
								File.WriteAllBytes(filePath + fileName, wwwImage.bytes);
								curAdData.isTextureFileCached = true;

								SaveIASData();
							} catch(IOException e){
								GoogleAnalytics.Instance.IASLogError("Failed to create cache file! " + e.Message);
								throw;
							}

							// Dispose of the wwwImage data as soon as we no longer need it (clear it from memory)
							wwwImage.Dispose();
						}

						curAdData.adTextureId = advertTextures.Count - 1;
						curAdData.isTextureReady = true;
						curAdData.isDownloading = false;
					}
				}

				//if(advancedLogging)
				//	Debug.Log("Finished ad download of " + curAdData.packageName);

				if(OnIASImageDownloaded != null)
					OnIASImageDownloaded.Invoke();
			}

			// Wait a frame between each ad we preload
			yield return null;
		}
	}

	private int GetUniqueUsableAdCount(int jsonFileId, int wantedSlotInt)
	{
		List<AdData> allSlotAds = advertData[jsonFileId].slotInts[wantedSlotInt-1].advert;

		List<string> processedPackageNames = new List<string>();

		for(int i=0;i < allSlotAds.Count;i++)
		{
			if(!allSlotAds[i].isSelf && allSlotAds[i].isActive){
				bool packageNameAlreadyProcessed = false;

				foreach(string package in processedPackageNames){
					if(allSlotAds[i].packageName == package)
						packageNameAlreadyProcessed = true;
				}

				if(!packageNameAlreadyProcessed)
					processedPackageNames.Add(allSlotAds[i].packageName);
			}
		}

		return processedPackageNames.Count;
	}

	private char GetSlotChar(int jsonFileId, int wantedSlotInt, int offset = 0, List<AdJsonFileData> customData = null)
	{
		AdSlotData curSlotData = GetAdSlotData(jsonFileId, wantedSlotInt, customData);

		if(curSlotData != null){
			int finalOffset = 0;

			if(customData == null){
				// Make sure all ads within preloadPackageNames are unique, otherwise fallback to showing ads already installed then fallback to allowing duplicates
				List<string> preloadPackageNames = new List<string>();

				int totalAds = curSlotData.advert.Count;

				for(int i=0;i <= offset && finalOffset < (totalAds * 2);)
				{
					// Manual modulo to support negative numbers
					int slotCharIdCheck = Mathf.Abs((curSlotData.lastSlotId + 1) + i + finalOffset) % curSlotData.advert.Count;
					char slotCharCheck = (char)(slotCharIdCheck + slotIdDecimalOffset);

					AdData curAd = GetAdDataByChar(jsonFileId, wantedSlotInt, slotCharCheck);

					bool packageNameCollision = false;

					foreach(string package in preloadPackageNames){
						if(curAd.packageName == package)
							packageNameCollision = true;
					}

					if(curAd.isSelf || !curAd.isActive || (finalOffset <= totalAds && curAd.isInstalled))
						packageNameCollision = true;

					if(!packageNameCollision){
						preloadPackageNames.Add(curAd.packageName);
						i++;
					} else {
						finalOffset++;
					}
				}
			}

			int wantedSlotCharId = ((curSlotData.lastSlotId + 1) + finalOffset + offset) % curSlotData.advert.Count; // If we use modulo here then empty ad slots would have ads but it would mean duplicate ads could appear on the backscreen together

			if(advancedLogging)
				Debug.Log("WantedSlotCharId: " + wantedSlotCharId);

			// Remove this to allow duplicate ads to show on the backscreen when there's not enough ads in the slot to fill it
			if(GetUniqueUsableAdCount(jsonFileId, wantedSlotInt) < offset && wantedSlotCharId - (offset-1) < 0){
				return default(char);
			} else {
				char wantedSlotChar = (char)(wantedSlotCharId + slotIdDecimalOffset);

				return wantedSlotChar;
			}
		} else {
			return default(char);
		}
	}

	private IEnumerator DownloadIASData(bool cachedDataLoaded = false)
	{
		// Wait for an active internet connection
		if(Application.internetReachability == NetworkReachability.NotReachable)
			yield return null;

		if(advancedLogging)
			Debug.Log("IAS downloading data..");

		List<AdJsonFileData> newAdvertData = new List<AdJsonFileData>();

		List<bool> needToDownloadAdSlot = new List<bool>();

		// Iterate through each JSON file
		for(int jsonFileId=0;jsonFileId < jsonUrls.Length;jsonFileId++)
		{
			needToDownloadAdSlot.Add(!cachedDataLoaded);

			// Download the JSON file
			WWW wwwJSON = new WWW (ConvertToSecureProtocol(jsonUrls[jsonFileId]));

			// Wait for the JSON data to be downloaded
			yield return wwwJSON;

			// Check for any errors
			if(!string.IsNullOrEmpty(wwwJSON.error)){
				GoogleAnalytics.Instance.IASLogError("JSON download error! " + wwwJSON.error, false);

				if(advancedLogging)
					Debug.LogError("JSON download error! " + wwwJSON.error);

				yield break;
			} else if(wwwJSON.text.Contains("There was an error")){
				GoogleAnalytics.Instance.IASLogError("JSON download error! Serverside system error!", false);

				if(advancedLogging)
					Debug.LogError("JSON download error! Serverside system error!");

				yield break;
			} else if(string.IsNullOrEmpty(wwwJSON.text)){
				GoogleAnalytics.Instance.IASLogError("JSON download error! Empty JSON!", false);

				if(advancedLogging)
					Debug.LogError("JSON download error! Empty JSON!");

				yield break;
			}

			JsonFileData tempAdvertData = new JsonFileData();

			try {
				#if UNITY_5 || UNITY_2017 || UNITY_2018
					tempAdvertData = JsonUtility.FromJson<JsonFileData>(wwwJSON.text);
				#else
					// Older version of Unity need the data manually mapped to the JsonFileData class
					tempAdvertData.slots = new List<JsonSlotData>();
					JSONNode jsonData = JSON.Parse(wwwJSON.text);

					for(int slotId=0;slotId < jsonData["slots"].AsArray.Count;slotId++)
					{
						JSONNode curSlot = jsonData["slots"].AsArray[slotId];
						JsonSlotData curSlotData = new JsonSlotData();

						curSlotData.slotid = curSlot["slotid"];
						curSlotData.updatetime = long.Parse(curSlot["updatetime"]);

						curSlotData.active = curSlot["active"].AsBool;

						curSlotData.adurl = curSlot["adurl"];
						curSlotData.imgurl = curSlot["imgurl"];

						tempAdvertData.slots.Add(curSlotData);
					}
				#endif
			} catch(ArgumentException e){
				GoogleAnalytics.Instance.IASLogError("JSON data invalid!" + e.Message, false);

				if(advancedLogging)
					Debug.LogError("JSON data invalid!" + e.Message);

				yield break;
			}

			// Dispose of the wwwJSON data (clear it from memory)
			wwwJSON.Dispose();

			if(tempAdvertData == null){
				GoogleAnalytics.Instance.IASLogError("Temp advert data was null!", false);

				if(advancedLogging)
					Debug.LogError("Temp advert data was null!");

				yield break;
			}

			if(tempAdvertData.slots.Count <= 0){
				GoogleAnalytics.Instance.IASLogError("Temp advert data has no slots!", false);

				if(advancedLogging)
					Debug.LogError("Temp advert data has no slots!");

				yield break;
			}

			if(!DoesSlotFileIdExist(jsonFileId, newAdvertData))
				newAdvertData.Add(new AdJsonFileData());

			bool needToRandomizeSlot = false;

			// We're currently only using the slots, not containers
			for(int i=0;i < tempAdvertData.slots.Count;i++)
			{
				try {
					JsonSlotData curSlot = tempAdvertData.slots[i];

					// We'll be converting the slot id (e.g 1a, 1c or 2f) into just number and just character values
					int slotInt; char slotChar;

					// Attempt to extract the slot int from the slot id
					if(!int.TryParse(Regex.Replace(curSlot.slotid, "[^0-9]", ""), out slotInt)){
						GoogleAnalytics.Instance.IASLogError("Failed to parse slot int from '" + curSlot.slotid + "'");

						if(advancedLogging)
							Debug.LogError("Failed to parse slot int from '" + curSlot.slotid + "'");

						yield break;
					}

					// Attempt to extract the slot character from the slot id
					if(!char.TryParse(Regex.Replace(curSlot.slotid, "[^a-z]", ""), out slotChar)){
						GoogleAnalytics.Instance.IASLogError("Failed to parse slot char from '" + curSlot.slotid + "'");

						if(advancedLogging)
							Debug.LogError("Failed to parse slot char from '" + curSlot.slotid + "'");

						yield break;
					}

					// If this slot doesn't exist yet create a new slot for it
					if(!DoesSlotIntExist(jsonFileId, slotInt, newAdvertData))
						newAdvertData[jsonFileId].slotInts.Add(new AdSlotData(slotInt, new List<AdData>()));

					// Get the index in the list for slotInt
					int slotDataIndex = GetSlotIndex(jsonFileId, slotInt, newAdvertData);

					if(slotDataIndex < 0){
						GoogleAnalytics.Instance.IASLogError("Failed to get slotDataIndex!");

						if(advancedLogging)
							Debug.LogError("Failed to get slotDataIndex!");

						yield break;
					}

					// Make sure this slot char isn't repeated in the json file within this slot int for some reason
					if(!DoesSlotCharExist(jsonFileId, slotInt, slotChar, newAdvertData)){
						newAdvertData[jsonFileId].slotInts[slotDataIndex].advert.Add(new AdData(slotChar));
					}

					if(advertData.Count >= (jsonFileId + 1) && advertData[jsonFileId].slotInts.Count >= (slotDataIndex + 1)){
						newAdvertData[jsonFileId].slotInts[slotDataIndex].lastSlotId = advertData[jsonFileId].slotInts[slotDataIndex].lastSlotId;
					} else {
						needToRandomizeSlot = true;
					}

					int slotAdIndex = GetAdIndex(jsonFileId, slotInt, slotChar, newAdvertData);

					if(slotAdIndex < 0){
						GoogleAnalytics.Instance.IASLogError("Failed to get slotAdIndex! Could not find " + slotInt + ", " + slotChar.ToString());

						if(advancedLogging)
							Debug.LogError("Failed to get slotAdIndex! Could not find " + slotInt + ", " + slotChar.ToString());

						yield break;
					}

					AdData curAdData = newAdvertData[jsonFileId].slotInts[slotDataIndex].advert[slotAdIndex];

					// Extract the bundleId of the advert
					#if UNITY_ANDROID
						// Regex extracts the id GET request from the URL which is the package name of the game
						// (replaces everything that does NOT match id=blahblah END or NOT match id=blahblah AMERPERSAND
						string packageName = Regex.Match(curSlot.adurl, "(?<=id=)((?!(&|\\?)).)*").Value;
					#elif UNITY_IOS
						// IOS we just need to grab the name after the hash in the URL
						string packageName = Regex.Match(curSlot.adurl, "(?<=.*#).*").Value;
					#else
						// For other platforms we should be fine to just use the full URL for package name comparisons as we'll be using .Compare
						// And other platforms won't include any other referral bundle ids in their URLs
						string packageName = curSlot.adurl;
					#endif

					string imageFileType = Regex.Match(curSlot.imgurl, "(?<=/uploads/adverts/.*)\\.[A-z]*[^(\\?|\")]").Value;

					curAdData.fileName = curSlot.slotid + imageFileType;
					curAdData.isSelf = packageName.Contains(bundleId);
					curAdData.isActive = curSlot.active;
					curAdData.isInstalled = IsPackageInstalled(packageName);
					curAdData.adUrl = curSlot.adurl;
					curAdData.packageName = packageName;

					curAdData.imgUrl = curSlot.imgurl;

					// Check if the cached active data needs the ad textures reloading
					if(advertData.Count >= (jsonFileId + 1) && advertData[jsonFileId].slotInts.Count >= (slotDataIndex + 1) && advertData[jsonFileId].slotInts[slotDataIndex].advert.Count >= (slotAdIndex + 1)){
						AdData activeCachedAdData = advertData[jsonFileId].slotInts[slotDataIndex].advert[slotAdIndex];

						if(activeCachedAdData.newUpdateTime < curSlot.updatetime || activeCachedAdData.newUpdateTime == 0L){
							needToDownloadAdSlot[jsonFileId] = true;
						} else {
							curAdData.adTextureId = activeCachedAdData.adTextureId;
							curAdData.isTextureReady = activeCachedAdData.isTextureReady;
							curAdData.lastUpdated = activeCachedAdData.lastUpdated;
							curAdData.isTextureFileCached = activeCachedAdData.isTextureFileCached;
							curAdData.isDownloading = activeCachedAdData.isDownloading;
						}
					} else {
						needToDownloadAdSlot[jsonFileId] = true;
					}

					curAdData.newUpdateTime = curSlot.updatetime;

					newAdvertData[jsonFileId].slotInts[slotDataIndex].advert[slotAdIndex] = curAdData;

					// I'm not pre-downloading all the images here because it takes quite a long time to download even on our fast ethernet connection (~15 seconds)
					// So I think it's best to download the images (if needed) when the ads are called to be refreshed
				} catch(ArgumentNullException e){
					if(advancedLogging)
						Debug.LogError("Missing slot parameter! " + e.Message);

					continue;
				}

				// Wait a frame between each ad we process (lots of regex aint cheap)
				yield return null;
			}

			if(needToRandomizeSlot)
				RandomizeAdSlots(jsonFileId, newAdvertData);
		}

		advertData = DeepCopyAsJsonFileData(newAdvertData);

		// Do this after updating the advertData so were working with live values
		for(int jsonFileId=0;jsonFileId < jsonUrls.Length;jsonFileId++){
			if(needToDownloadAdSlot[jsonFileId])
				RefreshActiveAdSlots(jsonFileId);
		}

		SaveIASData();

		if(advancedLogging)
			Debug.Log("IAS Done");
	}

	// Save the IAS data as the user quit the app (as saving whenever the data is updated is expensive)
	// OnApplicationQuit isn't always called as the player may just minimize then kill the app when
	// Or on iOS the app is suspended (calling OnApplicationPause(true)) unless "Exit on suspend" is enabled
	void OnApplicationQuit()
	{
		if(hasQuitForceSaveBeenCalled) return;

		SaveIASData(true);
		hasQuitForceSaveBeenCalled = true;
	}

	void OnApplicationPause(bool pauseState)
	{
		if(pauseState){
			if(hasQuitForceSaveBeenCalled) return;

			SaveIASData(true);
			hasQuitForceSaveBeenCalled = true;
		} else {
			hasQuitForceSaveBeenCalled = false;
		}
	}

	/// <summary>
	/// Call this for every IAS advert the player views
	/// </summary>
	/// <param name="packageName">Advert package name</param>
	/// <param name="isBackscreen">Is this a backscreen advert</param>
	public static void OnImpression(string packageName, bool isBackscreen)
	{
		if(Instance.logAdImpressions)
			GoogleAnalytics.Instance.IASLogEvent("IAS Views", Instance.bundleId + " " + (isBackscreen ? "(backscreen)" : "(main)"), packageName);
	}

	/// <summary>
	/// Call this for every IAS advert the player clicks
	/// </summary>
	/// <param name="packageName">Advert package name</param>
	/// <param name="isBackscreen">Is this a backscreen advert</param>
	public static void OnClick(string packageName, bool isBackscreen)
	{
		if(Instance.logAdClicks)
			GoogleAnalytics.Instance.IASLogEvent("IAS Clicks", Instance.bundleId + " " + (isBackscreen ? "(backscreen)" : "(main)"), packageName);
	}

	/// <summary>
	/// Refreshes the IAS adverts
	/// </summary>
	/// <param name="jsonFileId">JSON file ID</param>
	/// <param name="wantedSlotInt">Slot int</param>
	public static void RefreshBanners(int jsonFileId, int wantedSlotInt, bool forceChangeActive = false, List<AdJsonFileData> customData = null)
	{
		if(!Instance.DoesSlotIntExist(jsonFileId, wantedSlotInt, customData)){
			#if UNITY_EDITOR
				Debug.Log("(Editor Only) Attempted to refresh a banner slot which was either blacklisted or not yet ready! (Slot " + wantedSlotInt + ") This will do nothing");
			#endif

			if(Instance.advancedLogging)
				Debug.Log("Refresh failed, blacklisted? Slot " + wantedSlotInt);

			return;
		}

		if(Instance.advancedLogging)
			Debug.Log("Refreshing banners for jsonFileId:" + jsonFileId + ", wantedSlotInt: " + wantedSlotInt + " has custom data? " + (customData != null ? "YES" : "NO"));

		Instance.IncSlotChar(jsonFileId, wantedSlotInt, customData);

		if(forceChangeActive){
			if(OnForceChangeWanted != null){
				OnForceChangeWanted.Invoke();
			}
		}
	}

	/// <summary>
	/// Returns whether the ad texture has downloaded or not
	/// </summary>
	/// <returns><c>true</c> if is ad ready the specified jsonFileId wantedSlotInt; otherwise, <c>false</c>.</returns>
	/// <param name="jsonFileId">JSON file ID</param>
	/// <param name="wantedSlotInt">Slot int</param>
	public static bool IsAdReady(int jsonFileId, int wantedSlotInt, int offset = 0)
	{
		AdData returnValue = Instance.GetAdData(jsonFileId, wantedSlotInt, offset);

		if(returnValue != null){
			return returnValue.isTextureReady;
		} else {
			return false;
		}
	}

	/// <summary>
	/// Returns the URL of the current active advert from the requested JSON file and slot int
	/// </summary>
	/// <returns>The advert URL</returns>
	/// <param name="jsonFileId">JSON file ID</param>
	/// <param name="wantedSlotInt">Slot int</param>
	public static string GetAdURL(int jsonFileId, int wantedSlotInt, int offset = 0)
	{
		AdData returnValue = Instance.GetAdData(jsonFileId, wantedSlotInt, offset);

		if(returnValue != null){
			return returnValue.adUrl;
		} else {
			return string.Empty;
		}
	}

	/// <summary>
	/// Returns the package name of the current active advert from the requested JSON file and slot int
	/// </summary>
	/// <returns>The advert package name</returns>
	/// <param name="jsonFileId">JSON file ID</param>
	/// <param name="wantedSlotInt">Slot int</param>
	public static string GetAdPackageName(int jsonFileId, int wantedSlotInt, int offset = 0)
	{
		AdData returnValue = Instance.GetAdData(jsonFileId, wantedSlotInt, offset);

		if(returnValue != null){
			return returnValue.packageName;
		} else {
			return string.Empty;
		}
	}

	/// <summary>
	/// Returns the Texture of the current active advert from the requested JSON file and slot int
	/// </summary>
	/// <returns>The advert texture</returns>
	/// <param name="jsonFileId">JSON file ID</param>
	/// <param name="wantedSlotInt">Slot int</param>
	public static Texture GetAdTexture(int jsonFileId, int wantedSlotInt, int offset = 0)
	{
		AdData returnValue = Instance.GetAdData(jsonFileId, wantedSlotInt, offset);

		if(returnValue != null){
			return Instance.advertTextures[returnValue.adTextureId];
		} else {
			return null;
		}
	}

}