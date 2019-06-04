using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WebSocketSharp;
using UnityEngine.Events;



public class PopMessage
{
	public bool HasBeenHandled = false;
}

public class PopMessageBinary : PopMessage
{
	public byte[] Data = null;

	public PopMessageBinary(byte[] _Data)
	{
		Data = _Data;
	}
};


public class PopMessageText : PopMessage
{
	public string Data = null;

	public PopMessageText(string _Data)
	{
		Data = _Data;
	}

	public T FromJson<T>()
	{
		return JsonUtility.FromJson<T>(Data);
	}
};

[System.Serializable]
public class UnityEvent_PopMessageBinary : UnityEvent<PopMessageBinary> { }

[System.Serializable]
public class UnityEvent_PopMessageText : UnityEvent<PopMessageText> { }

[System.Serializable]
public class UnityEvent_Hostname : UnityEvent<string> { }

[System.Serializable]
public class UnityEvent_HostnameError : UnityEvent<string, string> { }


public class KinectClient : MonoBehaviour
{
	[Header("First argument of event is host:port. Second is error")]
	public UnityEvent_Hostname OnConnecting;
	public UnityEvent_Hostname OnConnected;
	[Header("Invoked to match failed initial connect")]
	public UnityEvent_HostnameError OnDisconnected;

	public UnityEvent_PopMessageBinary OnMessageBinary;
	public UnityEvent_PopMessageText OnMessageText;

	public bool EnableDebug = true;

	//	move these jobs to a thread!
	[Range(1, 5000)]
	public int MaxJobsPerFrame = 1000;

	WebSocket Socket;
	bool SocketConnecting = false;
	bool DebugUpdate = false;
	public int DefaultPort = 1234;

	public List<string> Hosts = new List<string>() { "localhost" };
	int CurrentHostIndex = 0;
	public string CurrentHost
	{
		get
		{
			try
			{
				return AddPortToHostname(Hosts[CurrentHostIndex % Hosts.Count], DefaultPort);
			}
			catch { }
			return null;
		}
	}


	[Range(0, 10)]
	public float RetryTimeSecs = 5;
	private float RetryTimeout = 1;

	//	websocket commands come on a different thread, so queue them for the next update
	List<System.Action> JobQueue;


	string AddPortToHostname(string Hostname, int Port)
	{
		//	not very robust atm, improve when required
		if (Hostname.Contains(":"))
			return Hostname;
		else
			return Hostname + ":" + Port;
	}

	public void Debug_Log(string Message)
	{
		Debug.Log(Message);
	}

	public void Connect(string NewHost)
	{
		if (Hosts==null)
			Hosts = new List<string>();

		Hosts.Add(NewHost);
		//CurrentHostIndex = 0;
		Connect();
	}

	//	gr: change this to throw on immediate error
	void Connect()
	{
		//	already connected
		if (Socket != null)
			return;

		if (SocketConnecting)
			return;

		var Host = CurrentHost;
		if (Host == null)
		{
			OnDisconnected.Invoke(null, "No hostname specified");
			return;
		}
		CurrentHostIndex++;

		Debug_Log("Connecting to " + Host + "...");
		OnConnecting.Invoke(Host);

		//	any failure from here should have a corresponding fail
		try
		{
			var NewSocket = new WebSocket("ws://" + Host);
			SocketConnecting = true;
			//NewSocket.Log.Level = LogLevel.TRACE;

			NewSocket.OnOpen += (sender, e) =>
			{
				QueueJob(() =>
				{
					SocketConnecting = false;
					Socket = NewSocket;
					OnConnected.Invoke(Host);
				});
			};

			NewSocket.OnError += (sender, e) =>
			{
				QueueJob(() =>
				{
					OnError(Host, e.Message, true);
				});
			};

			NewSocket.OnClose += (sender, e) =>
			{
				SocketConnecting = false;
				/*
				if ( LastConnectedHost != null ){
					QueueJob (() => {
						SetStatus("Disconnected from " + LastConnectedHost );
					});
				}
				*/
				OnError(Host, "Closed", true);
			};

			//	gr: does this need to be a queued job?
			//	gr: it does now, 2017 throws because of use of the events
			NewSocket.OnMessage += (sender, e) =>
			{

				System.Action Handler = () =>
				{
					if (e.Type == Opcode.TEXT)
						OnTextMessage(e.Data);
					else if (e.Type == Opcode.BINARY)
						OnBinaryMessage(e.RawData);
					else
						OnError(Host, "Unknown opcode " + e.Type, false);
				};
				QueueJob(Handler);
			};

			//	socket assigned upon success
			NewSocket.ConnectAsync();
		}
		catch (System.Exception e)
		{
			SocketConnecting = false;
			if (Socket != null)
			{
				Debug.LogWarning("Unexpected non-null socket");
				Socket = null;
			}
			OnDisconnected.Invoke(Host, e.Message);
		}
	}

	void Update()
	{

		/*
		if (Socket != null && !Socket.IsAlive) {
			OnError ("Socket not alive");
			Socket.Close ();
			Socket = null;
		}
*/
		if (Socket == null)
		{

			if (RetryTimeout <= 0)
			{
				Connect();
				RetryTimeout = RetryTimeSecs;
			}
			else
			{
				RetryTimeout -= Time.deltaTime;
			}
		}

		//	commands to execute from other thread
		if (JobQueue != null)
		{
			var JobsExecutedCount = 0;
			while (JobQueue.Count > 0 && JobsExecutedCount++ < MaxJobsPerFrame)
			{

				if (DebugUpdate)
					Debug.Log("Executing job 0/" + JobQueue.Count);
				var Job = JobQueue[0];
				JobQueue.RemoveAt(0);
				try
				{
					Job.Invoke();
					if (DebugUpdate)
						Debug.Log("Job Done.");
				}
				catch (System.Exception e)
				{
					Debug.Log("Job invoke exception: " + e.Message);
				}
			}

			if (JobQueue.Count > 0)
			{
				Debug.LogWarning("Executed " + JobsExecutedCount + " this frame, " + JobQueue.Count + " jobs remaining");
			}
		}

	}



	public void OnTextMessage(string Message)
	{
		if (EnableDebug)
			Debug_Log ("Text message: " + Message.Substring (0, 40));
		var Msg = new PopMessageText(Message);
		OnMessageText.Invoke(Msg);
	}

	public void OnBinaryMessage(byte[] Message)
	{
		if ( EnableDebug )
			Debug_Log ("Binary Message: " + Message.Length + " bytes");
		var Msg = new PopMessageBinary(Message);
		OnMessageBinary.Invoke(Msg);
	}

	void OnError(string Host, string Message, bool Close)
	{
		Debug_Log(Host + " error: " + Message);
		OnDisconnected.Invoke(Host, Message);

		if (Close)
		{
			if (Socket != null)
			{

				//	recurses if we came here from on close
				if (Socket.IsAlive)
					Socket.Close();
				Socket = null;
				SocketConnecting = false;
			}
		}
	}


	void OnApplicationQuit()
	{

		if (Socket != null)
		{
			//	if (Socket.IsAlive)
			Socket.Close();
		}

	}

	void QueueJob(System.Action Job)
	{
		if (JobQueue == null)
			JobQueue = new List<System.Action>();
		JobQueue.Add(Job);
	}

	public void Send(byte[] Data, System.Action<bool> OnDataSent = null)
	{
		if (Socket == null)
			throw new System.Exception("Not connected");

		Socket.SendAsync(Data, OnDataSent);
	}

	public void Send(string Data, System.Action<bool> OnDataSent = null)
	{
		if (Socket == null)
			throw new System.Exception("Not connected");

		Socket.SendAsync(Data, OnDataSent);
	}

}
