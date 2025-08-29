using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Cysharp.Threading.Tasks;
using SuikaBilliards.GameFlow.UI;
using SuikaBilliards.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ClientPlugin;

class APSetup : Ticker {
	readonly static string[] tags = ["DeathLink"];
	
	static GameOptionSteamUI gosui;
	static bool optionsOpen;
	static uint lh = 0;

    public override void Tick() {
        if (gosui) {
			bool newOO = gosui.transform.GetChild(0).gameObject.activeSelf && !LocalMultiPlayManager.Instance.IsJoinRemotePlayer;
			if (newOO && !optionsOpen) {
				OnOptionsOpened();
			} else if (!newOO && optionsOpen)
				OnOptionsClosed();
			optionsOpen = newOO;
		} else {
			gosui = Component.FindObjectOfType<GameOptionSteamUI>();
		}

		if (applySetting) {
			var u = applySetting.GetComponent<FocusableButtonUI>();
			if (!u.m_button.IsHighlighted())
				u.m_onPointerEnterEvent.Invoke(u);
		}

		if (archipelagoButtonImage) {
			if (archipelagoButtonButton.IsPressed())
				OnAPClicked();
			else {
				if (archipelagoButtonButton.IsHighlighted())
					lh = 2;
				else if (lh > 0)
					lh--;
				archipelagoButtonImage.sprite = lh > 0 ? Plugin.archipelagoIconYellow : Plugin.archipelagoIcon;
			}
		}

		if (popup != null)
			ph.HandlePipe();
		
		if (!optionsOpen && Plugin.archipelagoSession != null)
			Remove();
    }

	static GameObject archipelagoButton;
	static Image archipelagoButtonImage;
	static Button archipelagoButtonButton;
	static GameObject connectPage;
	static TextMeshProUGUI textField;
	static Transform applySetting;
	static readonly List<Transform> moved = [];

	static string configFile;
	static Process popup;
	static NamedPipeServerStream server;
	static PipeHandler ph;

	static void OnOptionsOpened() {
		if (!Plugin.archipelagoIcon)
			Plugin.LoadData();

		archipelagoButton = GameObject.Instantiate(
			gosui.transform.Find("Root/FocusableButton_Return").gameObject,
			new(4.83f, -3.7f, 1f),
			Quaternion.identity,
			gosui.transform.GetChild(0)
		);
		archipelagoButton.transform.localScale = new(1.5f, 1.5f, 1.5f);
		(archipelagoButtonImage = archipelagoButton.transform.Find("Root/Base/Icon").GetComponent<Image>()).sprite = Plugin.archipelagoIcon;

		var fbui = archipelagoButton.GetComponent<FocusableButtonUI>();
		fbui.m_highlightedColor = Color.white;
		archipelagoButtonButton = fbui.m_button;
		var cb = archipelagoButtonButton.colors;
		cb.highlightedColor = fbui.m_highlightedColor;
		archipelagoButtonButton.colors = cb;
	}

	static void OnOptionsClosed() {
		if (archipelagoButton)
			GameObject.Destroy(archipelagoButton);
		
		if (connectPage)
			GameObject.Destroy(connectPage);

		foreach (var t in moved)
			if (t.localPosition.y > 32768f)
				t.localPosition += new Vector3(0f, -65536f, 0f);
		
		moved.Clear();

		if (applySetting) {
			var ev = applySetting.GetComponent<FocusableButtonUI>().m_submitEvent;
			if (ev[0] == null)
				ev.RemoveAt(0);
			applySetting = null;
		}

		if (server != null) {
			if (server.IsConnected)
				server.Disconnect();
			server.Dispose();
		}
	}

	static void OnAPClicked() {
		GameObject.Destroy(archipelagoButton);

		applySetting = GameObject.Find("Apply_Frame").transform;
		applySetting.localPosition += new Vector3(0f, 65536f, 0f);
		moved.Add(applySetting);
		applySetting = GameObject.Find("Apply Setting").transform;
		applySetting.localPosition += new Vector3(0f, 65536f, 0f);
		applySetting.GetComponent<FocusableButtonUI>().m_submitEvent.Insert(0, null);
		moved.Add(applySetting);

		ph = new();

		var sc = gosui.transform.Find("Root/Window/SelectMenu/SelectableContents");
		connectPage = GameObject.Instantiate(
			sc.gameObject,
			new(),
			Quaternion.identity,
			sc.parent
		);
		connectPage.transform.localPosition = sc.localPosition;
		sc.localPosition += new Vector3(0f, 65536f, 0f);
		moved.Add(sc);

		string exe = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ConnectUI.exe");
		bool exeExists = File.Exists(exe);

		var field = connectPage.transform.GetChild(0);
		Component.Destroy(field.GetComponent<FocusableSideLanguageListUI>());
		GameObject.Destroy(field.Find("Root/Arrow").gameObject);
		GameObject.Destroy(field.Find("Root/Icon").gameObject);
		textField = field.GetComponentInChildren<TextMeshProUGUI>();
		textField.GetComponent<RectTransform>().sizeDelta = textField.transform.parent.GetComponent<RectTransform>().rect.size;
		textField.transform.localPosition = new(textField.GetComponent<RectTransform>().sizeDelta.x * -0.46f, textField.transform.localPosition.y, 0f);
		textField.text = exeExists ? "Please wait" : "Popup not found. Check if it was eaten by your antivirus.";
		textField.alignment = TextAlignmentOptions.Center;

		for (int i = connectPage.transform.GetChildCount() - 1; i >= 1; i--)
			GameObject.Destroy(connectPage.transform.GetChild(i).gameObject);
		
		if (exeExists) {
			StringBuilder pipeNameBuilder = new(12);

			for (int i = 0; i < 12; i++)
				pipeNameBuilder.Append((char)('a' + UnityEngine.Random.Range(0, 25)));

			var pipeName = pipeNameBuilder.ToString() + "_htm";

			configFile = Path.Combine(Application.persistentDataPath, "archipelago.cfg");

			server = new(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
			server.BeginWaitForConnection(res => {
				Plugin.L("Pipe established");
				ph.connected = true;
				server.EndWaitForConnection(res);
			}, null);

			string defaultHost = "archipelago.gg";
			string defaultPort = "38281";
			string defaultSlot = "";
			string defaultPassword = "";

			try {
				var config = File.ReadAllLines(configFile);
				defaultHost = config[0].Trim();
				defaultPort = config[1].Trim();
				defaultSlot = config[2].Trim();
				defaultPassword = config[3].Trim();
			} catch {}

			popup = Process.Start(
				exe, $@"{pipeName} /{defaultHost} {defaultPort} /{
					Convert.ToBase64String(Encoding.UTF8.GetBytes(defaultSlot))} /{Convert.ToBase64String(Encoding.UTF8.GetBytes(defaultPassword))}"
			);
		}
	}

	static void TryConnectAP(string host, int port, string slot, string password) {
		Plugin.L($"Creating Archipelago session {slot}@{host}:{port}");

		var session = ArchipelagoSessionFactory.CreateSession(host, port);

		session.Socket.ErrorReceived += (error, _) => Plugin.Log.LogError(error);
		Console.Listen(session);

		LoginResult result;
		try {
			result = session.TryConnectAndLogin("Hololive Treasure Mountain", slot, ItemsHandlingFlags.AllItems, tags: tags, password: password);
		} catch (Exception e) {
			result = new LoginFailure(e.GetBaseException().Message);
		}

		if (result.Successful) {
			Plugin.L("Archipelago session connected");

			APSlot.instance = ((LoginSuccessful)result).SlotData.ToObject<APSlot>();
			Plugin.Start(session);

			if (server.IsConnected)
				server.Disconnect();
			
			popup = null;

			var u = applySetting.GetComponent<FocusableButtonUI>();
			if (u.m_submitEvent[0] == null)
				u.m_submitEvent.RemoveAt(0);
			u.Submit();
		} else {
			var failure = (LoginFailure)result;

			Plugin.L("Archipelago server rejected connection:");
			foreach (var error in failure.Errors)
				Plugin.L("  " + error);

			SendError("Failed to connect to Archipelago room\0");
		}
	}

	static void SendError(string error) {
		var buffer = Encoding.UTF8.GetBytes(error);
		server.Write(buffer, 0, buffer.Length);
		server.Flush();
	}

	class PipeHandler {
		float popupConnectTimeout;
		public bool connected = false;
		bool maybeHasMessage = false;
		bool shownMessage = false;
		byte[] activeRead = null;
		readonly List<byte> message = [];

		public void HandlePipe() {
			popupConnectTimeout += Time.deltaTime;

			if (!connected && popupConnectTimeout > 120) {
				Plugin.L(textField.text = "Timeout connecting to Archipelago settings UI");
				popup.Kill();
				popup = null;
				return;
			}

			if ((connected && !server.IsConnected) || popup.HasExited) {
				OnOptionsClosed();
				optionsOpen = false;
				popup = null;
				return;
			}

			if (connected && !shownMessage) {
				shownMessage = true;
				textField.text = "Continue in popup";
			}

			if (connected && activeRead == null) {
				activeRead = new byte[64];
				server.BeginRead(activeRead, 0, 64, res => {
					message.AddRange(activeRead.Take(server.EndRead(res)));
					activeRead = null;
					maybeHasMessage = true;
				}, null);
			}

			if (maybeHasMessage) {
				maybeHasMessage = false;
				int length = message.IndexOf(0);
				if (length >= 0) {
					Plugin.L("Received pipe message");

					var items = Encoding.UTF8.GetString([.. message.Take(length)]).Split('\n');
					message.RemoveRange(0, length + 1);

					File.WriteAllLines(configFile, items);
					TryConnectAP(items[0], int.Parse(items[1]), items[2], items[3] == "" ? null : items[3]);
				}
			}
		}
	}
}