using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IAS_Handler : MonoBehaviour {

	public int jsonFileId = 0;
	[FormerlySerializedAs("bannerID")]
	public int adTypeId = 1; // 1 = Square, 2 = Tall

	private UITexture selfTexture;

	private string activeUrl;
	private string activePackageName;

	private bool isTextureAssigned = false;

	void Awake()
	{
		selfTexture = GetComponent<UITexture>();
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

	#if UNITY_EDITOR
		void Update()
		{
			if(Input.GetKeyDown(KeyCode.R)){
				isTextureAssigned = false;

				IAS_Manager.RefreshBanners(jsonFileId, adTypeId);
			}
		}
	#endif

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

			selfTexture.mainTexture = adTexture;
			isTextureAssigned = true;

			IAS_Manager.OnImpression(activePackageName);
		}
	}

	void OnClick()
	{
		if(selfTexture != null && !string.IsNullOrEmpty(activeUrl)){
			IAS_Manager.OnClick(activePackageName);

			Application.OpenURL(activeUrl);
		}
	}

}
