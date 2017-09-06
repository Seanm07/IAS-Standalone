using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class IAS_HandlerCanvas : MonoBehaviour 
{	
	public int jsonFileId = 0;
	[UnityEngine.Serialization.FormerlySerializedAs("bannerID")]
	public int adTypeId = 1; // 1 = Square, 2 = Tall
	public int adOffset = 0; // Used for backscreen ads (0, 1, 2)

	private string activeUrl;
	private string activePackageName;

	private bool isTextureAssigned = false;
	
	private Button selfButton;
	private Image selfImage;

	void Awake()
	{
		selfImage = GetComponent<Image>();

		if(selfImage){
			selfButton = gameObject.AddComponent<Button>();
			selfButton.onClick.AddListener(OnMouseUp);
		}
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
		if(!isTextureAssigned && IAS_Manager.IsAdReady(jsonFileId, adTypeId, adOffset)){
			Texture2D adTexture = IAS_Manager.GetAdTexture(jsonFileId, adTypeId, adOffset) as Texture2D;
			activeUrl = IAS_Manager.GetAdURL(jsonFileId, adTypeId, adOffset);
			activePackageName = IAS_Manager.GetAdPackageName(jsonFileId, adTypeId, adOffset);

			selfImage.sprite = Sprite.Create(adTexture, new Rect(0f, 0f, adTexture.width, adTexture.height), new Vector2(adTexture.width / 2f, adTexture.height / 2f));
			isTextureAssigned = true;

			IAS_Manager.OnImpression(activePackageName); // DO NOT REMOVE THIS LINE
		}
	}

	void OnMouseUp()
	{
		if(selfImage != null && !string.IsNullOrEmpty(activeUrl)){
			IAS_Manager.OnClick(activePackageName); // DO NOT REMOVE THIS LINE

			Application.OpenURL(activeUrl);
		}
	}
}
