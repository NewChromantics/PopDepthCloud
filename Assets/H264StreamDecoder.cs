using System.Collections;
using System.Collections.Generic;
using UnityEngine;



[System.Serializable]
public class UnityEvent_Texture : UnityEngine.Events.UnityEvent<Texture> { }


[System.Serializable]
public class PacketMeta
{
	public float HorizontalFov = 0;
	public float VerticalFov = 0;
	public float DiagonalFov = 0;
	public int MinReliableDistance = 0;
	public int MaxReliableDistance = 65535;
	public int Time = 0;			//	time of frame from host
	public int RelativeTime = 0;    //	from kinect sdk
	public int FrameIndex = 0;
	public int DepthMin = 0;        //	quantisized data min 
	public int DepthMax = 0;        //	quantisized data max
}


public class H264StreamDecoder : MonoBehaviour
{
	PopH264.Decoder Decoder;
	public UnityEvent_Texture OnDepthTextureUpdated;
	int FrameNumber = 0;
	public bool EnableDebug = true;
	public Material CloudMaterial;
	PacketMeta LastPacketMeta = null;
	public Camera ProjectionCamera;
	public bool UseCameraProjection = false;

	[Range(0.001f, 100.0f)]
	public float WorldNear = 0.01f;
	[Range(0.001f, 100.0f)]
	public float WorldFar = 1.00f;


	public void OnTextData(PopMessageText Message)
	{
		//	need to keep this in sync with packet numbers
		var NewMeta = JsonUtility.FromJson<PacketMeta>(Message.Data);
		LastPacketMeta = NewMeta;
	}

	public void OnBinaryData(PopMessageBinary DataMessage)
	{
		DecodeH264(DataMessage.Data);
	}

	void DecodeH264(byte[] Data)
	{
		if (Decoder==null)
			Decoder = new PopH264.Decoder();

		Decoder.PushFrameData(Data, FrameNumber++);
		
	}

	List<Texture2D> Planes;
	List<PopH264.SoyPixelsFormat> PlaneFormats;

	void Update()
	{
		if (Decoder == null)
			return;

		var NewFrameNumber = Decoder.GetNextFrame(ref Planes, ref PlaneFormats);
		if (!NewFrameNumber.HasValue)
			return;
		if (EnableDebug)
			Debug.Log(this.name + " decoded frame " + NewFrameNumber.Value + ". Planes x" + Planes.Count);

		OnDepthTextureUpdated.Invoke(Planes[0]);

		//	update material
		if (CloudMaterial!=null && LastPacketMeta!=null)
		{
			UpdateMaterial(CloudMaterial, LastPacketMeta, Planes[0]);
		}
	}

	void UpdateMaterial(Material Material, PacketMeta Meta, Texture Texture)
	{
		Material.mainTexture = Texture;

		var Aspect = Meta.HorizontalFov / Meta.VerticalFov;

		//	setup camera for visualisation
		if (ProjectionCamera)
		{
			//	gr: fov is vertical
			//		aspect is width / height
			//	https://docs.unity3d.com/ScriptReference/Matrix4x4.Perspective.html
			ProjectionCamera.fieldOfView = Meta.VerticalFov;
			ProjectionCamera.aspect = Aspect;
			ProjectionCamera.nearClipPlane = WorldNear;
			ProjectionCamera.farClipPlane = WorldFar;
		}

		//var ProjectionMatrix = ProjectionCamera ? c: Matrix4x4.identity;
		var ProjectionMatrix = Matrix4x4.Perspective(Meta.VerticalFov, Aspect, WorldNear, WorldFar);

		if (UseCameraProjection)
			ProjectionMatrix = ProjectionCamera.projectionMatrix;
 		Material.SetMatrix("DepthProjectionMatrix", ProjectionMatrix);
		Material.SetFloat("TextureDepthMin", Meta.DepthMin);
		Material.SetFloat("TextureDepthMax", Meta.DepthMax);
		Material.SetFloat("CameraDepthMin", Meta.MinReliableDistance);
		Material.SetFloat("CameraDepthMax", Meta.MaxReliableDistance);
		Material.SetFloat("WorldDepthMin", WorldNear);
		Material.SetFloat("WorldDepthMax", WorldFar);
	}

}
