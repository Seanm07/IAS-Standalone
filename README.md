# IAS Manager Documentation

## Pre-notes:
The new IAS script now supports multiple json urls instead of a json file for the main and backscreen banners, generally you'll only be using 1 json file but in some cases multiple may be used. You interact with each json file using the order of assignment in the inspector for the jsonUrls array e.g the first json file is 0 then 1 then 2 etc.

wantedSlotInt variables refer to the advert type, so 1 is square, 2 is tall - this isn't an enum so we can possibly add more sizes in future if they're needed but really you'll only be referring to 1 and 2

When refreshing adverts make sure to only refresh what you need, e.g don't refresh square AND tall if only tall is going to be visible after the refresh call; the new system is built so ads aren't downloaded or loaded into memory when they don't need to be. 

Note! If you're not using a certain size banner it's still best to blacklist them (see blacklisting step in setup section) because 1 ad of all sizes is still preloaded ready to be displayed AND restarting the app jumps ads to the next in queue so eventually all ads would be on the disk.

## IAS Plugin Setup
- Download our java plugin [Pickle_GetPackages.jar](http://data.i6.com/IAS/GamePickle/Pickle_GetPackages.jar) 
- Put Pickle_GetPackages.jar inside **/Plugins/Android/** (create the folders if they don't exist)
- Download [JarLoader.cs](http://data.i6.com/IAS/GamePickle/JarLoader.cs) (Script which interacts with the Java plugin)
- Attach JarLoader.cs to a persistent gameobject in your initial scene (put this on a gameobject which is never destroyed)

### Unity 4 / early 5 only
- If you are using Unity 4 or an early version of Unity 5 download [SimpleJSON.cs](http://pastebin.com/38gyz4mB) (Plugin which parses the JSON data, later versions of Unity have a built in JSON parser)
- Put SimpleJSON.cs inside **/Plugins/**

## IAS Manager Script Setup:
- Import IAS_Manager.cs
- Attach IAS_Manager.cs to a Game Object which will never be destroyed in the earliest scene possible (ads can start loading as soon as the script is active) make sure also never to create a duplicate of the script, you will be given an error if you do
- Fill in the inspector values
- Import whichever IAS Handler you need for your UI system (Currently we have Unity Canvas, NGUI and  GUITexture)
- Attach the IAS Handler to a GameObject with either a UITexture, GUITexture or Canvas Image and set the Json File Id and Ad Type Id (See pre-notes which explains these)
- If the ad is displayed on a screen where multiple ads will appear (such as the backscreens) then tick the Refresh After Place option to make each advert different
- **Blacklisting slots** *Optional* If you don't need a certain image size then you can completely disable them by blacklisting their id via the IAS_Manager inspector under blacklisted slots, for example add a value of 2 to disable tall banners from loading

## Scripting:
### Logging Impressions
```c#
IAS_Manager.OnImpression(string packageName);
```
Call this when an advert becomes visible to the user, you can grab the package name of adverts from GetAdPackageName(int jsonFileId, int wantedSlotInt) which will return a formatted package name for the platform

**Example usage:**
```c#
IAS_Manager.OnImpression(IAS_Manager.GetAdPackageName(0, 1));
```
This would log an impression on the current active square advert (id: 1) for the first json file (id: 0)

---

### Logging Clicks
```c#
IAS_Manager.OnClick(string packageName);
```
Call this when an advert is clicked by the user, you can grab the package name of adverts from GetAdPackageName(int jsonFileId, int wantedSlotInt) which will return a formatted package name for the platform

**Example usage:**
```c#
IAS_Manager.OnClick(IAS_Manager.GetAdPackageName(0, 1));
```
This would log a click on the current active square advert (id: 1) for the first json file (id: 0)

---

### Refreshing Adverts
```c#
IAS_Manager.RefreshBanners(int jsonFileId, int wantedSlotInt, bool forceChangeActive = false);
```
Refreshes the IAS advert of the requested json file id and slot id, if forceChangeActive is true then the callback OnForceChangeWanted will trigger. Note! The image might not instantly be ready so afterwards you should also wait for the OnIASImageDownloaded callback!

**Example usage:**
```c#
IAS_Manager.RefreshBanners(0, 1);
```
This will refresh the square banner (id: 1) for the first json file (id: 0)

---

### Checking Advert States
```c#
IAS_Manager.IsAdReady(int jsonFileId, int wantedSlotInt);
```
Returns a bool on whether the texture for the requested ad is ready. Note: You don't need to rely on checking this constantly, instead just use the OnIASImageDownloaded callback followed by calling this function to make sure the downloaded image was the wanted ad.

**Example usage:**
```c#
if(IAS_Manager.IsAdReady(0, 2))
```
This will check if the tall banner (id: 2) for the first json file (id: 0) is loaded

---

### Get Advert URL
```c#
IAS_Manager.GetAdURL(int jsonFileId, int wantedSlotInt);
```
Returns the string of the advert URL, this URL should be opened when the ad is clicked

**Example usage:**
```c#
Application.OpenURL(IAS_Manager.GetAdURL(0, 1));
```
Opens the store URL of the active square banner (id: 1) for the first json file (id: 0)

---

### Get Advert Package Name
```c#
IAS_Manager.GetAdPackage(int jsonFileId, int wantedSlotInt);
```
Returns the formatted package name of the requested advert, this works with all platforms including iOS

**Example usage:**
```c#
IAS_Manager.GetAdPackage(0, 2);
```
Returns the package name of the active tall banner (id: 2) for the first json file (id: 0)

---

### Get Advert Texture
```c#
IAS_Manager.GetAdTexture(int jsonFileId, int wantedSlotInt);
```
Returns the texture for the requested advert. Note: You should check IAS_Manager.IsAdReady(..) before calling this, see the IsAdReady section for more info.

**Example usage:**
```c#
void OnEnable()
{
   IAS_Manager.OnIASImageDownloaded += OnAdReady;
}

void OnDisable()
{
   IAS_Manager.OnIASImageDownloaded -= OnAdReady;
}

private void OnAdReady()
{
   if(IAS_Manager.IsAdReady(0, 1)){
      Texture adTexture = IAS_Manager.GetAdTexture(0, 1);
   }
}
```
If the advert texture is ready then it sets adTexture as the texture of the loaded square banner (id: 1) from the first json file (id: 0)

## Scripting Callbacks
### Advert Image Finished Downloading
```c#
IAS_Manager.OnIASImageDownloaded
```
Called each time an image has finished downloading, you should check the advert directly from this callback with IAS_Manager.IsAdReady(..) just to make sure the ad which finished downloaded in the one you expected to be downloaded

**Example usage:**
```c#
void OnEnable()
{
   IAS_Manager.OnIASImageDownloaded += OnAdReady;
}

void OnDisable()
{
   IAS_Manager.OnIASImageDownloaded -= OnAdReady;
}

private void OnAdReady()
{
   if(IAS_Manager.IsAdReady(0, 1)){
      // Set the texture here
   }
}
```

---

### Advert Forced to Reload
```c#
IAS_Manager.OnForceChangeWanted
```
Called after a banner refresh is called with forceChangeActive set to true, this allows you to change IAS adverts without needing any special code, e.g you might want banners on a certain screen to change every x minutes or have a floating advert which is used across multiple screens which is force refreshed

```c#
void OnEnable()
{
   IAS_Manager.OnForceChangeWanted += OnForceChangeWanted;
}

void OnDisable()
{
   IAS_Manager.OnForceChangeWanted -= OnForceChangeWanted;
}

private void OnForceChangeWanted()
{
   // Code here to force reset the banner texture to be changed
}
```
Make sure the advert texture is actually ready again before just applying a new texture, it's probably wise to wait for both OnIASImageDownloaded AND OnForceChangeWanted before changing the IAS texture for a forced ad change.
