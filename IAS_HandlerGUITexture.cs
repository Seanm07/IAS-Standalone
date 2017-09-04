using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IAS_Handler : MonoBehaviour {

	public int jsonFileId = 0;
	[UnityEngine.Serialization.FormerlySerializedAs("bannerID")]
	public int adTypeId = 1; // 1 = Square, 2 = Tall

	private GUITexture selfTexture;

	private string activeUrl;
	private string activePackageName;

	private bool isTextureAssigned = false;

	void Awake()
	{
		selfTexture = GetComponent<GUITexture>();
	}

	void OnEnable()
	{
		IAS_Manager.OnIASImageDownloaded += OnIASReady;
		IAS_Manager.OnForceChangeWanted += OnIASForced;

		SetupAdvert();
	}

	void OnDisable()
	{
		IAS_Manager.OnIASImageDownloaded -= OnIASReady;
		IAS_Manager.OnForceChangeWanted -= OnIASForced;

		isTextureAssigned = false; // Allows the texture on this IAS ad to be replaced
	}

	void Update()
	{
		if(Input.GetKeyDown(KeyCode.R)){
			isTextureAssigned = false;

			IAS_Manager.RefreshBanners(jsonFileId, adTypeId);
		}
	}

	private void OnIASReady()
	{
		SetupAdvert();
	}

	private void OnIASForced()
	{
		isTextureAssigned = false;

		SetupAdvert();
	}

	private void SetupAdvert()
	{
		if(!isTextureAssigned && IAS_Manager.IsAdReady(jsonFileId, adTypeId)){
			Texture adTexture = IAS_Manager.GetAdTexture(jsonFileId, adTypeId);
			activeUrl = IAS_Manager.GetAdURL(jsonFileId, adTypeId);
			activePackageName = IAS_Manager.GetAdPackageName(jsonFileId, adTypeId);

			selfTexture.texture = adTexture;
			isTextureAssigned = true;

			IAS_Manager.OnImpression(activePackageName); // DO NOT REMOVE THIS LINE
		}
	}

	void OnClick()
	{
		if(selfTexture != null && !string.IsNullOrEmpty(activeUrl)){
			IAS_Manager.OnClick(activePackageName); // DO NOT REMOVE THIS LINE

			Application.OpenURL(activeUrl);
		}
	}

}