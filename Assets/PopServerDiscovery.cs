using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//	gr: this won't work for UWP/hololens
using System.Net.Sockets;
using System.Net;
using System.Text;
using System;


[System.Serializable]
public struct PopServerDiscoverResponse
{
	public string[] Addresses;
}

public class PopServerDiscovery : MonoBehaviour 
{
	public bool						AutoConnectOnDiscovery = true;
	public bool						DisableOnDiscovery = true;
	public UnityEvent_Hostname		OnDiscoveredHost;

	public string	BroadcastString = "hello";
	public int		BroadcastPort = 9999;
	UdpClient		Socket;
	IPEndPoint		EndPoint;
	bool			SentBroadcast = false;

	[Range(0.5f,20.0f)]
	public float	BroadcastEveryXSeconds = 5;
	float			BroadcastCountdown = 0;

	//	multithread, queue results :/
	List<string>	FoundHosts = null;

	[Header("If include list isn't empty, it MUST match one. If any exclude matched, it's excluded.")]
	public List<string> HostFilterExclude;
	public List<string> HostFilterInclude;

	void OnEnable()
	{
		Socket = new UdpClient();
		EndPoint = new IPEndPoint(IPAddress.Broadcast, BroadcastPort);
		Socket.BeginReceive(new AsyncCallback(Receive), null);
		FoundHosts = null;
	}

	void OnDisable()
	{
		Socket.Close();
	}

	void Update()
	{
		BroadcastCountdown -= Time.deltaTime;
		if (BroadcastCountdown <= 0) 
		{
			Broadcast ();
			BroadcastCountdown = BroadcastEveryXSeconds;
		}
	
		if (FoundHosts != null) {
			foreach (var Hostname in FoundHosts) {
				try
				{
					OnFoundHost(Hostname);
				}
				catch(System.Exception e) {
					Debug.LogException (e);
				}
			}
		}

	}

	void OnFoundHost(string Hostname)
	{
		Debug.Log("Found host " + Hostname);
		OnDiscoveredHost.Invoke (Hostname);

		if ( DisableOnDiscovery )
			this.enabled = false;
	}


	void Broadcast() 
	{
		//	still waiting for previous
		if (SentBroadcast)
			return;
		
		byte[] data = Encoding.ASCII.GetBytes(BroadcastString);
		SentBroadcast = true;
		Socket.Send(data, data.Length, EndPoint);
	}


	void OnRecieveHost(string Host)
	{
		if (FoundHosts == null)
			FoundHosts = new List<string>();

		//	check it's a match
		bool Included = true;
		bool Excluded = false;

		if (HostFilterInclude != null && HostFilterInclude.Count > 0)
		{
			Included = false;
			foreach (var Match in HostFilterInclude)
				if (Host.Contains(Match))
					Included = true;
		}

		if (HostFilterExclude != null)
		{
			foreach (var Match in HostFilterExclude)
				if (Host.Contains(Match))
					Excluded = true;
		}

		if (Included && !Excluded)
			FoundHosts.Add(Host);
		else
			Debug.LogWarning("Excluded host " + Host);
	}

	void Receive(System.IAsyncResult Result)
	{
		//	terminate this session to get the data
		byte[] received = Socket.EndReceive(Result, ref EndPoint);
		SentBroadcast = false;
		var Reply = Encoding.ASCII.GetString(received);

		try
		{
			var Response = JsonUtility.FromJson<PopServerDiscoverResponse>(Reply);

			//	have to queue this for the mono thread in order to do anything useful
			foreach (var Address in Response.Addresses)
				OnRecieveHost(Address);
		}
		catch (System.Exception e)
		{
			Debug.LogException(e);
			OnRecieveHost(Reply);
		}
	}


}