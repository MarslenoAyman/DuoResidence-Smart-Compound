// SyncManager.cs
// WebSocket link between the real compound and its digital twin.
// Both sides connect to the Node.js relay server (Server/server.js, port 8080),
// and the server passes every message from one side to the other.
//
// The same script runs on both sides. The isRealSide setting picks the role:
//
//   REAL SIDE (isRealSide = true)
//     Sends the state of every street light 10 times per second
//     (brightness, power, mode and battery).
//     Receives commands from the twin and applies them to the real lights.
//
//   TWIN SIDE (isRealSide = false)
//     Receives the light states and mirrors them on the twin lights.
//     When the user takes manual control of a light, or sets it back to Auto,
//     from the light dashboard, it sends that command to the real side.
//
// Messages are small JSON texts, built and read by hand,
// so the project needs no extra JSON library.
// Start the server first with:  node server.js
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class SyncManager : MonoBehaviour
{
    // -------------------------------------------------------
    // SETTINGS (editable in the Inspector)
    // -------------------------------------------------------

    [Header("Sync Settings")]
    // True on the real compound, false on the digital twin
    public bool isRealSide = true;

    // Address of the relay server (both sides run on the same PC)
    public string serverAddress = "ws://localhost:8080";

    // Seconds between two light state messages (0.1 = 10 times per second)
    public float sendInterval = 0.1f;

    // Name of this side, shown in the server log
    public string clientLabel = "Real Side";


    // -------------------------------------------------------
    // CONNECTION STATUS (shown in the Inspector)
    // -------------------------------------------------------

    [Header("Connection Status")]
    public bool   isConnected     = false;
    public int    packetsSent     = 0;
    public int    packetsReceived = 0;
    public string lastError       = "";


    // -------------------------------------------------------
    // SHARED ACCESS AND CONSTANTS
    // -------------------------------------------------------

    // The single sync manager in the scene, used by the light dashboard
    public static SyncManager Instance { get; private set; }

    // Size of the receive buffer (64 KB is enough for all 120 lights)
    private const int RECEIVE_BUFFER_SIZE = 65536;

    // Numbers are always written and read with a dot as the decimal sign,
    // whatever the language settings of the PC are
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;


    // -------------------------------------------------------
    // RUNTIME STATE
    // -------------------------------------------------------

    private ClientWebSocket webSocket;
    private CancellationTokenSource cancelSource;

    // Messages arrive on a background thread and are applied on the main thread,
    // so they wait in this queue, protected by a lock
    private readonly Queue<string> receivedMessages = new Queue<string>();
    private readonly object queueLock = new object();

    // Every street light, plus a lookup by light name for fast matching
    private LightController[] allLights;
    private readonly Dictionary<string, LightController> lightsById = new Dictionary<string, LightController>();

    // Time since the last light state message
    private float sendTimer = 0f;

    // Number of the last light state message (the server logs every 60th one)
    private int sequenceNumber = 0;


    // -------------------------------------------------------
    // UNITY EVENTS
    // -------------------------------------------------------

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        allLights = FindObjectsByType<LightController>();
        foreach (LightController light in allLights)
            lightsById[light.gameObject.name] = light;

        StartConnection();
    }

    void Update()
    {
        ApplyReceivedMessages();

        // Only the real side sends light states, on a fixed interval
        if (!isRealSide) return;

        sendTimer += Time.deltaTime;
        if (sendTimer >= sendInterval)
        {
            sendTimer = 0f;
            SendLightStates();
        }
    }

    // Stops the background work and closes the connection when the scene ends
    void OnDestroy()
    {
        if (cancelSource != null) cancelSource.Cancel();

        if (webSocket != null && webSocket.State == WebSocketState.Open)
            _ = webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Scene ending", CancellationToken.None);

        if (webSocket != null) webSocket.Dispose();
    }


    // -------------------------------------------------------
    // CONNECTION
    // -------------------------------------------------------

    // Connects to the server, tells it which side this is, then starts listening
    async void StartConnection()
    {
        cancelSource = new CancellationTokenSource();
        webSocket    = new ClientWebSocket();

        try
        {
            Debug.Log("[SyncManager] Connecting to " + serverAddress + "...");
            await webSocket.ConnectAsync(new Uri(serverAddress), cancelSource.Token);

            isConnected = true;
            Debug.Log("[SyncManager] Connected as " + clientLabel);

            await SendRaw("{\"type\":\"identity\",\"label\":\"" + clientLabel + "\"}");
            _ = ReceiveLoop();
        }
        catch (Exception e)
        {
            lastError   = e.Message;
            isConnected = false;
            Debug.LogWarning("[SyncManager] Connection failed: " + e.Message);
            Debug.LogWarning("[SyncManager] Make sure server.js is running in a terminal first.");
        }
    }

    // Runs in the background and queues every message that arrives
    async Task ReceiveLoop()
    {
        byte[] buffer = new byte[RECEIVE_BUFFER_SIZE];

        while (webSocket.State == WebSocketState.Open)
        {
            try
            {
                WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancelSource.Token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Debug.Log("[SyncManager] Server closed connection");
                    break;
                }

                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                lock (queueLock)
                {
                    receivedMessages.Enqueue(message);
                    packetsReceived++;
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
                break;
            }
            catch (Exception e)
            {
                lastError = e.Message;
                Debug.LogWarning("[SyncManager] Receive error: " + e.Message);
                break;
            }
        }

        isConnected = false;
    }

    // Sends one JSON text to the server
    async Task SendRaw(string json)
    {
        if (webSocket == null || webSocket.State != WebSocketState.Open) return;

        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancelSource.Token);
        }
        catch (Exception e)
        {
            lastError = e.Message;
            Debug.LogWarning("[SyncManager] Send error: " + e.Message);
        }
    }


    // -------------------------------------------------------
    // REAL SIDE: SEND LIGHT STATES
    // -------------------------------------------------------

    // Builds one message with the state of every light and sends it
    void SendLightStates()
    {
        if (!isConnected || allLights == null) return;

        sequenceNumber++;

        var json = new StringBuilder();
        json.Append("{\"type\":\"lightStates\",\"seq\":").Append(sequenceNumber).Append(",\"lights\":[");

        bool first = true;
        foreach (LightController light in allLights)
        {
            if (light == null) continue;
            if (!first) json.Append(',');
            first = false;

            json.Append("{\"id\":\"").Append(light.gameObject.name)
                .Append("\",\"brightness\":").Append(light.currentBrightness.ToString("F3", Invariant))
                .Append(",\"watts\":").Append(light.currentWatts.ToString("F1", Invariant))
                .Append(",\"mode\":\"").Append(light.currentMode)
                .Append("\",\"battery\":").Append(light.batteryPercent.ToString("F1", Invariant))
                .Append('}');
        }
        json.Append("]}");

        // Sent without waiting, so the frame is never blocked
        _ = SendRaw(json.ToString());
        packetsSent++;
    }


    // -------------------------------------------------------
    // TWIN SIDE: SEND COMMANDS (called by the light dashboard)
    // -------------------------------------------------------

    // Tells the real side that a light was set to a manual brightness (0 to 1)
    public void SendOverrideCommand(string lightId, float brightness)
    {
        if (!isConnected || isRealSide) return;

        _ = SendRaw("{\"type\":\"command\",\"lightId\":\"" + lightId +
                    "\",\"brightness\":" + brightness.ToString("F3", Invariant) + "}");
    }

    // Tells the real side that a light was given back to Auto
    public void SendAutoCommand(string lightId)
    {
        if (!isConnected || isRealSide) return;

        _ = SendRaw("{\"type\":\"command\",\"lightId\":\"" + lightId + "\",\"action\":\"setAuto\"}");
    }


    // -------------------------------------------------------
    // APPLY RECEIVED MESSAGES (main thread)
    // -------------------------------------------------------

    // Takes every waiting message out of the queue and applies it
    void ApplyReceivedMessages()
    {
        while (true)
        {
            string message;
            lock (queueLock)
            {
                if (receivedMessages.Count == 0) return;
                message = receivedMessages.Dequeue();
            }
            ApplyMessage(message);
        }
    }

    // Light states go to the twin lights, commands go to the real lights
    void ApplyMessage(string json)
    {
        try
        {
            if (json.Contains("\"type\":\"lightStates\""))
                ApplyLightStates(json);
            else if (json.Contains("\"type\":\"command\""))
                ApplyCommand(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SyncManager] Parse error: " + e.Message);
        }
    }

    // Twin side: copies brightness and battery from every light in the message
    void ApplyLightStates(string json)
    {
        int searchFrom = 0;

        while (true)
        {
            // Each light entry starts with its "id" field
            int idIndex = json.IndexOf("\"id\":\"", searchFrom, StringComparison.Ordinal);
            if (idIndex < 0) break;

            int idStart = idIndex + 6;
            int idEnd   = json.IndexOf('"', idStart);
            if (idEnd < 0) break;

            string lightId   = json.Substring(idStart, idEnd - idStart);
            float brightness = ExtractFloat(json, "\"brightness\":", idEnd);
            float battery    = ExtractFloat(json, "\"battery\":", idEnd);

            if (lightsById.TryGetValue(lightId, out LightController light) && light != null)
            {
                // A light under manual control on the twin keeps its own brightness
                if (light.currentMode != LightController.LightMode.Manual)
                    light.currentBrightness = brightness;

                light.batteryPercent = battery;
            }

            searchFrom = idEnd + 1;
        }
    }

    // Real side: applies a manual brightness or Auto command to one light
    void ApplyCommand(string json)
    {
        string lightId = ExtractString(json, "\"lightId\":\"");
        if (!lightsById.TryGetValue(lightId, out LightController light) || light == null) return;

        if (json.Contains("\"action\":\"setAuto\""))
            light.SetToAuto();
        else
            light.SetManualBrightness(ExtractFloat(json, "\"brightness\":", 0));
    }


    // -------------------------------------------------------
    // SIMPLE JSON READERS
    // -------------------------------------------------------

    // Reads the number written after a key, starting the search at a position
    float ExtractFloat(string json, string key, int searchFrom)
    {
        int keyIndex = json.IndexOf(key, searchFrom, StringComparison.Ordinal);
        if (keyIndex < 0) return 0f;

        int valueStart = keyIndex + key.Length;
        int valueEnd   = valueStart;

        // A number is made of digits, a dot and a minus sign
        while (valueEnd < json.Length && (char.IsDigit(json[valueEnd]) || json[valueEnd] == '.' || json[valueEnd] == '-'))
            valueEnd++;

        string number = json.Substring(valueStart, valueEnd - valueStart);
        return float.TryParse(number, NumberStyles.Float, Invariant, out float result) ? result : 0f;
    }

    // Reads the text written after a key, up to the closing quote
    string ExtractString(string json, string key)
    {
        int keyIndex = json.IndexOf(key, StringComparison.Ordinal);
        if (keyIndex < 0) return "";

        int start = keyIndex + key.Length;
        int end   = json.IndexOf('"', start);
        return end < 0 ? "" : json.Substring(start, end - start);
    }
}
