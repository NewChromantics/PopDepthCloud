using System.Collections;
using System.Collections.Generic;
using UnityEngine;



[System.Serializable]
public class UnityEvent_Texture : UnityEngine.Events.UnityEvent<Texture> { }



public class H264StreamDecoder : MonoBehaviour
{
	PopH264.Decoder Decoder;
	public UnityEvent_Texture OnDepthTextureUpdated;
	int FrameNumber = 0;

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

	void Update()
	{
		if (Decoder == null)
			return;

		var	Planes = new List<Texture2D>();
		var PlaneFormats = new List<PopH264.SoyPixelsFormat>();
		var NewFrameNumber = Decoder.GetNextFrame(ref Planes, ref PlaneFormats);
		if (!NewFrameNumber.HasValue)
			return;
		Debug.Log("Decoded frame " + NewFrameNumber.Value + ". Planes x" + Planes.Count);
		OnDepthTextureUpdated.Invoke(Planes[0]);
	}

}
