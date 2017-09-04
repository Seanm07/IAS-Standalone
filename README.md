# IAS Manager Documentation

## Pre-notes:
The new IAS script now supports multiple json urls instead of a json file for the main and backscreen banners, generally you'll only be using 1 json file but in some cases multiple may be used. You interact with each json file using the order of assignment in the inspector for the jsonUrls array e.g the first json file is 0 then 1 then 2 etc.

wantedSlotInt variables refer to the advert type, so 1 is square, 2 is tall - this isn't an enum so we can possibly add more sizes in future if they're needed but really you'll only be referring to 1 and 2

When refreshing adverts make sure to only refresh what you need, e.g don't refresh square AND tall if only tall is going to be visible after the refresh call; the new system is built so ads aren't downloaded or loaded into memory when they don't need to be.

## Setup:
- Import IAS_Manager.cs
- Attach IAS_Manager.cs to a Game Object which will never be destroyed in the earliest scene possible (ads can start loading as soon as the script is active) make sure also never to create a duplicate of the script, you will be given an error if you do
- Fill in the inspector values

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
if(IAS_Manager.IsAdReady(0, 1)){
   Texture adTexture = IAS_Manager.GetAdTexture(0, 1);
}
```
If the advert texture is ready then it sets adTexture as the texture of the loaded square banner (id: 1) from the first json file (id: 0)