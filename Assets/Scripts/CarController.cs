using System;
using System.IO.Ports;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CarController : MonoBehaviour
{
    [Header("Bluetooth Connection")]
    public string portName = "COM7";
    public int baudRate = 9600;
    public bool connectOnStart = true;

    [Header("Control")]
    public float resendInterval = 0.15f;

    [Header("UI Buttons (opcional: si se dejan vacios se buscan por nombre)")]
    public Button upButton;
    public Button downButton;
    public Button leftButton;
    public Button rightButton;

    [Header("Connect / Disconnect Buttons (NUEVO)")]
    public Button connectButton;
    public Button disconnectButton;
    [Tooltip("Sprite del boton Conectar mientras hay conexion (opcional)")]
    public Sprite connectedSprite;
    [Tooltip("Sprite del boton Desconectar cuando esta en modo AUTO / desconectado (opcional)")]
    public Sprite disconnectedSprite;

    [Header("Status")]
    public string status = "Disconnected";
    public string lastCommand = "-";

    SerialPort serial;
    volatile bool connected;
    volatile bool connecting;
    volatile bool justConnected;
    volatile string connectionError;

    char sentCommand = '\0';
    float nextSendTime;

    // NUEVO
    char uiCommand = 'S';
    bool simulating;
    bool wHeld, sHeld, aHeld, dHeld;

    void Start()
    {
        SetupUIButtons();
        if (connectOnStart) Connect();
    }

    void Update()
    {
        if (connectionError != null)
        {
            Debug.LogError($"[Car] Could not open {portName}: {connectionError}");
            status = "Connection error (press C to retry)";
            connectionError = null;
        }

        if (justConnected)
        {
            justConnected = false;
            status = "Connected - MANUAL";
            Debug.Log($"[Car] Connected to {portName}");
            sentCommand = '\0';
            Send('S');
        }

        if (KeyPressed('F')) AutoMode();
        if (KeyPressed('C')) Connect();

        UpdateKeyVisuals();

        if (!connected) return;

        char desired = ReadMovement();
        if (desired != sentCommand || Time.time >= nextSendTime)
        {
            Send(desired);
        }
    }

    void OnApplicationQuit() => Close(true);
    void OnDestroy() => Close(true);

    public void Connect()
    {
        if (connected || connecting) return;
        connecting = true;
        status = "Connecting...";

        new Thread(() =>
        {
            try
            {
                var s = new SerialPort(portName, baudRate)
                {
                    WriteTimeout = 500,
                    DtrEnable = false,
                    RtsEnable = false
                };
                s.Open();
                serial = s;
                connected = true;
                justConnected = true;
            }
            catch (Exception e)
            {
                connectionError = e.Message;
            }
            finally
            {
                connecting = false;
            }
        }) { IsBackground = true }.Start();
    }

    public void AutoMode()
    {
        if (!connected)
        {
            Debug.LogWarning("[Car] Connect first (press C) to enable auto mode.");
            return;
        }
        Send('A');
        Thread.Sleep(150);
        Close(false);
        status = "AUTO (disconnected) - press C to take control";
        Debug.Log("[Car] Auto mode enabled, Bluetooth disconnected");
    }

    char ReadMovement()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k == null) return uiCommand;
        if (k.wKey.isPressed) return 'F';
        if (k.sKey.isPressed) return 'B';
        if (k.aKey.isPressed) return 'L';
        if (k.dKey.isPressed) return 'R';
#else
        if (Input.GetKey(KeyCode.W)) return 'F';
        if (Input.GetKey(KeyCode.S)) return 'B';
        if (Input.GetKey(KeyCode.A)) return 'L';
        if (Input.GetKey(KeyCode.D)) return 'R';
#endif
        return uiCommand;
    }

    bool KeyPressed(char key)
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k == null) return false;
        if (key == 'F') return k.fKey.wasPressedThisFrame;
        if (key == 'C') return k.cKey.wasPressedThisFrame;
        return false;
#else
        if (key == 'F') return Input.GetKeyDown(KeyCode.F);
        if (key == 'C') return Input.GetKeyDown(KeyCode.C);
        return false;
#endif
    }

    void Send(char command)
    {
        if (!connected || serial == null) return;
        try
        {
            serial.Write(command.ToString());
            sentCommand = command;
            lastCommand = command.ToString();
            nextSendTime = Time.time + resendInterval;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Car] Connection lost: {e.Message}");
            Close(false);
            status = "Connection lost (press C to reconnect)";
        }
    }

    void Close(bool stopCar)
    {
        if (serial != null && serial.IsOpen)
        {
            if (stopCar)
            {
                try { serial.Write("S"); } catch { }
            }
            try { serial.Close(); } catch { }
        }
        serial = null;
        connected = false;
        sentCommand = '\0';
        if (stopCar) status = "Disconnected";
    }

    void OnGUI()
    {
        GUI.Box(new Rect(10, 10, 330, 95),
            $"Car: {status}\n" +
            $"Last command: {lastCommand}\n" +
            "W A S D = move   |   F = auto mode\n" +
            "C = connect / back to manual");
    }
    void SetupUIButtons()
    {
        upButton    = PrepareButton(upButton,    "Up",    'F');
        downButton  = PrepareButton(downButton,  "Down",  'B');
        leftButton  = PrepareButton(leftButton,  "Left",  'L');
        rightButton = PrepareButton(rightButton, "Right", 'R');

        connectButton    = PrepareActionButton(connectButton,    new[] { "Connect", "Conectar", "Conexion", "Conexión" }, () => Connect());
        disconnectButton = PrepareActionButton(disconnectButton, new[] { "Disconnect", "Desconectar", "Desconexion", "Desconexión", "Auto" }, () => AutoMode());

        if (connectButton != null) connectNormalSprite = GetButtonImage(connectButton)?.sprite;
        if (disconnectButton != null) disconnectNormalSprite = GetButtonImage(disconnectButton)?.sprite;
    }
    Sprite connectNormalSprite, disconnectNormalSprite;
    bool cHeld, fHeld;

    Image GetButtonImage(Button b)
    {
        if (b == null) return null;
        var img = b.targetGraphic as Image;
        return img != null ? img : b.GetComponent<Image>();
    }

    Button PrepareActionButton(Button b, string[] names, Action onClick)
    {
        if (b == null)
        {
            foreach (var n in names)
            {
                var go = GameObject.Find(n);
                if (go == null) continue;
                b = go.GetComponent<Button>();
                if (b == null)
                {
                    b = go.AddComponent<Button>();
                    b.targetGraphic = go.GetComponent<Graphic>();
                }
                break;
            }
        }
        if (b == null) return null;
        b.onClick.AddListener(() => onClick());
        return b;
    }
    void UpdateConnectionSprites()
    {
        var cImg = GetButtonImage(connectButton);
        if (cImg != null && connectedSprite != null && connectNormalSprite != null)
            cImg.sprite = connected ? connectedSprite : connectNormalSprite;

        var dImg = GetButtonImage(disconnectButton);
        if (dImg != null && disconnectedSprite != null && disconnectNormalSprite != null)
            dImg.sprite = (!connected && status.StartsWith("AUTO")) ? disconnectedSprite : disconnectNormalSprite;
    }

    Button PrepareButton(Button b, string objectName, char command)
    {
        if (b == null)
        {
            var go = GameObject.Find(objectName);
            if (go == null) return null;
            b = go.GetComponent<Button>();
            if (b == null)
            {
                b = go.AddComponent<Button>();
                b.targetGraphic = go.GetComponent<Graphic>();
            }
        }

        var trigger = b.gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = b.gameObject.AddComponent<EventTrigger>();

        AddTrigger(trigger, EventTriggerType.PointerDown, () => { if (!simulating) uiCommand = command; });
        AddTrigger(trigger, EventTriggerType.PointerUp,   () => { if (!simulating && uiCommand == command) uiCommand = 'S'; });
        AddTrigger(trigger, EventTriggerType.PointerExit, () => { if (!simulating && uiCommand == command) uiCommand = 'S'; });
        return b;
    }

    void AddTrigger(EventTrigger trigger, EventTriggerType type, Action action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    bool KeyHeld(char key)
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k == null) return false;
        if (key == 'W') return k.wKey.isPressed;
        if (key == 'S') return k.sKey.isPressed;
        if (key == 'A') return k.aKey.isPressed;
        if (key == 'D') return k.dKey.isPressed;
        if (key == 'C') return k.cKey.isPressed;
        if (key == 'F') return k.fKey.isPressed;
        return false;
#else
        if (key == 'W') return Input.GetKey(KeyCode.W);
        if (key == 'S') return Input.GetKey(KeyCode.S);
        if (key == 'A') return Input.GetKey(KeyCode.A);
        if (key == 'D') return Input.GetKey(KeyCode.D);
        if (key == 'C') return Input.GetKey(KeyCode.C);
        if (key == 'F') return Input.GetKey(KeyCode.F);
        return false;
#endif
    }
    void UpdateKeyVisuals()
    {
        UpdateKeyVisual(upButton,    'W', ref wHeld);
        UpdateKeyVisual(downButton,  'S', ref sHeld);
        UpdateKeyVisual(leftButton,  'A', ref aHeld);
        UpdateKeyVisual(rightButton, 'D', ref dHeld);
        UpdateKeyVisual(connectButton,    'C', ref cHeld);
        UpdateKeyVisual(disconnectButton, 'F', ref fHeld);
        UpdateConnectionSprites();
    }

    void UpdateKeyVisual(Button b, char key, ref bool wasHeld)
    {
        bool now = KeyHeld(key);
        if (now == wasHeld) return;
        wasHeld = now;
        if (b == null || EventSystem.current == null) return;

        var data = new PointerEventData(EventSystem.current);
        simulating = true;
        if (now) ExecuteEvents.Execute(b.gameObject, data, ExecuteEvents.pointerDownHandler);
        else
        {
            ExecuteEvents.Execute(b.gameObject, data, ExecuteEvents.pointerUpHandler);
            EventSystem.current.SetSelectedGameObject(null);
        }
        simulating = false;
    }
}