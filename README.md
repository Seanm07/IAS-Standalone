# IAS Manager Documentation

## Updating to the new IAS (Version 32)
Replace your existing IAS_Handler.cs with the updated [Canvas](https://github.com/Seanm07/IAS-Standalone/blob/master/IAS_HandlerCanvas.cs) or [NGUI](https://github.com/Seanm07/IAS-Standalone/blob/master/IAS_HandlerNGUI.cs) handler.

Script syntax changes:
1. jsonFileId is no longer used in IAS function calls
2. adSize now uses the IASAdSize enum as IASAdSize.Square or IASAdSize.Tall

## Download the Pickle plugins!
Our pickle plugins are needed to use the IAS scripts [Download the latest Pickle Plugins here](https://github.com/Seanm07/pickle-plugin/releases/latest)

## Pre-notes
wantedSlotInt variables refer to the advert type, so 1 is square, 2 is tall - this isn't an enum so we can possibly add more sizes in future if they're needed but really you'll only be referring to 1 and 2

When refreshing adverts make sure to only refresh what you need, e.g don't refresh square AND tall if only tall is going to be visible after the refresh call; the new system is built so ads aren't downloaded or loaded into memory when they don't need to be. 

Note! If you're not using a certain size banner it's still best to blacklist them (see blacklisting step in setup section) because 1 ad of all sizes is still preloaded ready to be displayed AND restarting the app jumps ads to the next in queue so eventually all ads would be on the disk.

## IAS Plugin Setup
- Download our pickle plugins [PicklePlugins.zip](https://github.com/Seanm07/pickle-plugin/releases/latest) 
- Extract the files into your Assets folder
- Attach JarLoader.cs to a persistent gameobject in your initial scene ***(put this on a gameobject which is never destroyed)***

## IAS Manager Script Setup
- Import IAS_Manager.cs
- Attach IAS_Manager.cs to a Game Object which will never be destroyed in the earliest scene possible (ads can start loading as soon as the script is active) make sure also never to create a duplicate of the script, you will be given an error if you do
- Fill in the inspector values
- Import whichever IAS Handler you need for your UI system (Currently we have Unity Canvas, NGUI and  GUITexture)
- Attach the IAS Handler to a GameObject with either a UITexture, GUITexture or Canvas Image and set the Json File Id and Ad Type Id (See pre-notes which explains these)
- If the ad is displayed on a screen where multiple ads will appear (such as the backscreens) then tick the Refresh After Place option to make each advert different
- **Blacklisting slots** *Optional* If you don't need a certain image size then you can completely disable them by blacklisting their id via the IAS_Manager inspector under blacklisted slots, for example add a value of 2 to disable tall banners from loading

## Scripting:


### Refreshing Adverts
```c#
IAS_Manager.RefreshBanners(IASAdSize adSize, bool forceChangeActive = false);
```
Refreshes the IAS advert of the requested size, if forceChangeActive is true then the callback OnForceChangeWanted will trigger. Note! The image might not instantly be ready so afterwards you should also wait for the OnIASImageDownloaded callback!

**Example usage:**
```c#
IAS_Manager.RefreshBanners(IASAdSize.Square, true);
```
This will refresh the square banner and instantly change the ad texture when ready
---

### Checking Advert States
```c#
IAS_Manager.IsAdReady(IASAdSize adSize, int offset = 0);
```
Returns a bool on whether the texture for the requested ad is ready. Note: You don't need to rely on checking this constantly, instead just use the OnIASImageDownloaded callback followed by calling this function to make sure the downloaded image was the wanted ad.

**Example usage:**
```c#
if(IAS_Manager.IsAdReady(IASAdSize.Tall, 2))
```
This will check if the tall banner with offset 2 is loaded

---

### Get Advert URL
```c#
IAS_Manager.GetAdURL(IASAdSize adSize, int offset = 0);
```
Returns the string of the advert URL, this URL should be opened when the ad is clicked

**Example usage:**
```c#
Application.OpenURL(IAS_Manager.GetAdURL(IASAdSize.Square, 0));
```
Opens the store URL of the active square banner (id: 1) for the first json file (id: 0)

---

### Get Advert Package Name
```c#
IAS_Manager.GetAdPackage(IASAdSize adSize, int offset = 0);
```
Returns the formatted package name of the requested advert, this works with all platforms including iOS

**Example usage:**
```c#
IAS_Manager.GetAdPackage(IASAdSize.Square, 2);
```
Returns the package name of the active tall banner at the second offset

---

### Get Advert Texture
```c#
IAS_Manager.GetAdTexture(IASAdSize adSize, int offset = 0);
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
   if(IAS_Manager.IsAdReady(IASAdSize.Square, 2)){
      Texture adTexture = IAS_Manager.GetAdTexture(IASAdSize.Square, 2);
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
   if(IAS_Manager.IsAdReady(IASAdSize.Square, 2)){
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
