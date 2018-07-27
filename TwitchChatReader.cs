using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Text;
using System.Net.Sockets;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.Diagnostics;
using System.Runtime.InteropServices;

public class TwitchChatReader : MonoBehaviour {
	public string channel = "";
	public string nickname = "";
	public string oauth = "";
	public float selectionWindow = 15f;
	public Text chatBox;
	public Text commandText;
	public static string path = "C:\\Users\\buz_e\\Documents\\Unity Projects\\Unity Music";
	public GameObject musicNotation;

	private int port = 6667;
	private TcpClient client;
	private NetworkStream stream;
	private Scrollbar chatVertScroll;
	private float timer = 0f;
	private IEnumerator coroutine;
	static string res;
	public const int MAXPNAMELEN = 32;

	public struct MidiOutCaps{
		public short wMid;
		public short wPid;
		public int vDriverVersion;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAXPNAMELEN)]
		public string szPname;
		public short wTechnology;
		public short wVoices;
		public short wNotes;
		public short wChannelMask;
		public int dwSupport;
	}
	// MCI INterface
	[DllImport("winmm.dll")]
	private static extern long mciSendString(string command, StringBuilder returnValue, int returnLength, System.IntPtr winHandle);

	// Midi API
	[DllImport("winmm.dll")]
	private static extern int midiOutGetNumDevs();


	[DllImport("winmm.dll")]
	private static extern int midiOutGetDevCaps(System.Int32 uDeviceID, ref MidiOutCaps lpMidiOutCaps, System.UInt32 cbMidiOutCaps);


	[DllImport("winmm.dll")]
	private static extern int midiOutOpen(ref int handle, int deviceID, MidiCallBack proc, int instance, int flags);

	[DllImport("winmm.dll")]
	private static extern int midiOutShortMsg(int handle, int message);

	[DllImport("winmm.dll")]
	private static extern int midiOutClose(int handle);

	private delegate void MidiCallBack(int handle, int msg, int instance, int param1, int param2);

	static string Mci(string command){
		StringBuilder reply = new StringBuilder(256);
		mciSendString(command, reply, 256, System.IntPtr.Zero);
		return reply.ToString();
	}
	static void PlayMidi(){
		res = System.String.Empty;

		// set path to midi file here
		string filename = path + "\\temp.mid.mid";

		res = Mci("open \"" + filename + "\" alias music");
		res = Mci("play music");
	}
	void StopMidi(){
		res = Mci("close music");
	}
	void OnDestroy(){
		res = Mci("close music");
	}
	void OnDisable(){
		res = Mci("close music");
	}

	void Start () {
		var numDevs = midiOutGetNumDevs();
		MidiOutCaps myCaps = new MidiOutCaps();
		var res = midiOutGetDevCaps(0, ref myCaps, (System.UInt32)Marshal.SizeOf(myCaps));

		chatVertScroll = GameObject.Find ("Chat Scrollbar Vertical").GetComponent<Scrollbar>();
		client = new TcpClient("irc.chat.twitch.tv", port);
		stream = client.GetStream ();

		// Send the message to the connected TcpServer. 
		string loginstring = "PASS oauth:"+oauth+"\r\nNICK "+nickname+"\r\n";
		Byte[] login = System.Text.Encoding.ASCII.GetBytes(loginstring);
		stream.Write(login, 0, login.Length);

		// send message to join channel
		string joinstring = "JOIN " + "#" + channel + "\r\n";
		Byte[] join = System.Text.Encoding.ASCII.GetBytes(joinstring);
		stream.Write(join, 0, join.Length);
		coroutine = UpdateImage ();
		StartCoroutine(coroutine);
		PlayMidi ();
	}
	IEnumerator UpdateImage() {
		Texture2D tex;
		tex = new Texture2D(800, 1000, TextureFormat.DXT1, false);
		using (WWW www = new WWW(path+"\\temp.mid.png"))
		{
			yield return www;
			www.LoadImageIntoTexture(tex);
		}
		Sprite s = Sprite.Create(tex, new Rect(0.0f, 0.0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
		musicNotation.GetComponent<Image> ().sprite = s;
	}
	void RunCommand(String cmd) {
		commandText.text = cmd;
		try {
			Process myProcess = new Process();
			myProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
			myProcess.StartInfo.CreateNoWindow = true;
			myProcess.StartInfo.UseShellExecute = false;
			myProcess.StartInfo.FileName = path+"\\TwitchProcessor\\TwitchProcessor.exe";
			//myProcess.StartInfo.Arguments = "\""+cmd+"\"";
			myProcess.StartInfo.Arguments = "\"generate 4 measures\"";
			myProcess.EnableRaisingEvents = true;
			myProcess.Start();
			myProcess.WaitForExit();
			coroutine = UpdateImage ();
			print("loading image");
			StartCoroutine(coroutine);
			print("play midi");
			PlayMidi ();
		} catch (Exception e){
			print(e);        
		}
	}
	void Update() {
		timer += Time.deltaTime;
		if(timer > selectionWindow){
			StopMidi ();
			string[] selection = chatBox.text.Split('\n');
			print ("run Donya's file");
			RunCommand(selection[UnityEngine.Random.Range (0, selection.Length)]);
			chatBox.text = "";
			timer = 0f;
		}
		byte[] buffer = new byte[1024];
		int bytesRead = 0;
		int readingPosition = 0;
		while(stream.DataAvailable)    
		{
			bytesRead = stream.Read(buffer, readingPosition , buffer.Length);
			readingPosition += bytesRead;
			string str = Encoding.UTF8.GetString (buffer, 0, bytesRead);
			//keep connection alive if pinged
			if (str == "PING :tmi.twitch.tv\r\n") {
				string pongstring = "PONG :tmi.twitch.tv\r\n";
				Byte[] pong = System.Text.Encoding.ASCII.GetBytes (pongstring);
				stream.Write (pong, 0, pong.Length);
			} else {
				string[] message = str.Split(':');
				string[] preamble = message[1].Split(' ');
				string tochat = "";
				if (preamble[1] == "PRIVMSG")
				{
					string[] sendingUser = preamble[0].Split('!');
					tochat = sendingUser[0] + ": " + message[2];

					// sometimes the carriage returns get lost (??)
					if (tochat.Contains("\n") == false)
					{
						tochat = tochat + "\n";
					}
				}
				else if (preamble[1] == "JOIN")
				{
					string[] sendingUser = preamble[0].Split('!');
					tochat = "JOINED: " + sendingUser[0];
				}
				chatBox.text += "  "+tochat;
				chatVertScroll.value = 0;
			}
		}
	}
}