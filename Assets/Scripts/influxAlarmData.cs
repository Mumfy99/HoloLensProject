using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.Text.RegularExpressions;

/// <summary>
/// Pure data provider for InfluxDB alarm codes.
/// No UI dependencies - just queries and exposes alarm data.
/// Other scripts can read alarm status or subscribe to events.
/// </summary>
public class InfluxAlarmDataProvider : MonoBehaviour
{
    [Header("InfluxDB Settings")]
    public string influxUrl = "http://130.130.130.208:8086";
    public string orgId = "66751340bcb17c31";
    public string apiToken = "rxFXXk69LjakTyXY4TLCKbncEe6LNIBCqQ46SuUBVwV-CvLw3M6NsoI5h7TcbpKHn1FLd344ZiBrmv3pPFL1Rg==";


    [Header("Query Settings")]
    public string bucketName = "Sif 401";
    public string range = "-1h";
    public float refreshIntervalSeconds = 1f;
    public string measurement = "alarm_code";
    public string field = "alarm";

    
    [Header("Feeder Query Settings")]
    public string minFeederMeasurement = "minfeeder";
    public string bdFeederMeasurement = "bdfeeder";

    // ============================================
    // PUBLIC DATA - Accessible by any script
    // ============================================
    
    /// <summary>
    /// Current alarm code (0 = no alarm)
    /// </summary>
    public int CurrentAlarmCode { get; private set; } = 0;

    /// <summary>
    /// Current alarm description
    /// </summary>
    public string CurrentAlarmDescription { get; private set; } = "No alarm";

    /// <summary>
    /// Is there an active alarm? (true if alarm code != 0)
    /// </summary>
    public bool HasActiveAlarm { get; private set; } = false;

    /// <summary>
    /// Stores values for minfeeder_1 to minfeeder_5.
    /// Access via index 1-5 (e.g., MinFeederValues[1]). Index 0 is unused.
    /// </summary>
    public int[] MinFeederValues { get; private set; } = new int[6];

    /// <summary>
    /// Stores values for bdfeeder_1 to bdfeeder_5.
    /// Access via index 1-5 (e.g., BdFeederValues[1]). Index 0 is unused.
    /// </summary>
    public int[] BdFeederValues { get; private set; } = new int[6];

    /// <summary>
    /// Last time the alarm data was updated
    /// </summary>
    public System.DateTime LastUpdateTime { get; private set; }

    /// <summary>
    /// Was the last query successful?
    /// </summary>
    public bool IsConnected { get; private set; } = false;

    /// <summary>
    /// Last error message (if any)
    /// </summary>
    public string LastError { get; private set; } = "";

    // ============================================
    // EVENTS - Subscribe to these for reactive updates
    // ============================================

    /// <summary>
    /// Fires when alarm status changes (alarm code changed or alarm cleared)
    /// Parameters: (alarmCode, description, hasAlarm)
    /// </summary>
    public delegate void OnAlarmChanged(int alarmCode, string description, bool hasAlarm);
    public event OnAlarmChanged AlarmChanged;

    /// <summary>
    /// Fires on every data update (even if alarm hasn't changed)
    /// </summary>
    public delegate void OnAlarmUpdated();
    public event OnAlarmUpdated AlarmUpdated;

    /// <summary>
    /// Fires when connection status changes
    /// Parameters: (isConnected, errorMessage)
    /// </summary>
    public delegate void OnConnectionChanged(bool isConnected, string errorMessage);
    public event OnConnectionChanged ConnectionChanged;

    // ============================================
    // ALARM CODE DICTIONARY
    // ============================================
    
    private readonly Dictionary<int, string> alarmDescriptions = new Dictionary<int, string>
    {
        {0, "No alarm"},
        {1, "Missing feeder 1"},
        {2, "Missing feeder 2"},
        {3, "Missing feeder 3"},
        {4, "Missing feeder 4"},
        {5, "Missing feeder 5"},
        {6, "Missing pallets"},
        {7, "Missing round canisters"},
        {8, "Missing square canisters"},
        {9, "Missing hopper 1"},
        {10, "Missing hopper 2"},
        {11, "Missing hopper 3"},
        {12, "Missing blue pellets"},
        {13, "Missing red pellets"},
        {14, "Missing yellow pellets"},
        {15, "Missing hopper with blue pellets"},
        {16, "Missing hopper with red pellets"},
        {17, "Missing hopper with yellow pellets"},
        {18, "Missing water in tank"},
        {19, "Missing oil in tank"},
        {20, "Configure round in one of the lines"},
        {21, "Configure square in one of the lines"},
        {22, "Refill line 1 with canisters"},
        {23, "Refill line 2 with canisters"},
        {24, "Missing round feeder"},
        {25, "Missing square feeder"},
        {26, "Missing lids at 1st position"},
        {27, "Missing lids at 2nd position"},
        {29, "Missing labels"},
        {30, "Remove products from ramp"},
        {31, "Remove rejected pallets from blue bin"},
        {32, "Missing packs in loader 1"},
        {33, "BCR code unreadable, type manually"},
        {34, "Selected position is occupied, please reset"},
        {35, "Selected position is empty, please reset"},
        {36, "Robot is disabled, please enable to continue"},
        {37, "Security area broken"},
        {38, "Changing plastic film"},
        {42, "Cannot store product in warehouse"},
        {43, "Check product quality"},
        {44, "Wrong container"},
        {45, "Table error, please RESET station"},
        {46, "Table error, please POWER OFF station"},
        {47, "Linear error, please RESET station"},
        {48, "Linear error, please POWER OFF station"},
        {49, "Linear 1 error, please RESET station"},
        {50, "Linear 1 error, please POWER OFF station"},
        {51, "Linear 2 error, please RESET station"},
        {52, "Linear 2 error, please POWER OFF station"},
        {53, "Rotary error, please RESET station"},
        {54, "Rotary error, please POWER OFF station"},
        {55, "Gripper error, please RESET station"},
        {56, "Gripper error, please POWER OFF station"},
        {57, "Please enable robot and press reset"},
        {58, "Emergency status"},
        {59, "Dock nº1 is full"},
        {60, "Dock nº2 is full"},
        {61, "Dock nº3 is full"},
        {62, "Dock nº1 and nº2 are full"},
        {63, "Dock nº1 and nº3 are full"},
        {64, "Dock nº2 and nº3 are full"},
        {65, "All docks are full"}
    };

    private Coroutine pollCoroutine;

    // ============================================
    // UNITY LIFECYCLE
    // ============================================

    private void Start()
    {
        pollCoroutine = StartCoroutine(PollLoop());
    }

    private void OnDestroy()
    {
        if (pollCoroutine != null)
            StopCoroutine(pollCoroutine);
    }

    // ============================================
    // POLLING LOGIC
    // ============================================

    private IEnumerator PollLoop()
    {
        while (true)
        {
            yield return PollOnce();
            yield return PollFeeders();
            yield return new WaitForSeconds(refreshIntervalSeconds);
        }
    }

    private IEnumerator PollOnce()
    {
        string url = $"{influxUrl}/api/v2/query?orgID={orgId}";
        string fluxQuery = BuildFluxQuery();

        string fluxEscaped = fluxQuery
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "")
            .Replace("\n", "\\n");

        string jsonBody = "{\"query\":\"" + fluxEscaped + "\"}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();

            www.SetRequestHeader("Authorization", "Token " + apiToken);
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Accept", "application/csv");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                string error = $"Query error: {www.error}";
                Debug.LogError($"[{bucketName}] {error}");
                
                UpdateConnectionStatus(false, error);
                UpdateAlarmData(0); // Default to no alarm on error
                yield break;
            }

            string response = www.downloadHandler.text;
            string valueString = ExtractLastValueFromCsv(response);

            int alarmCode = 0;
            if (!string.IsNullOrEmpty(valueString))
            {
                int.TryParse(valueString, NumberStyles.Integer, CultureInfo.InvariantCulture, out alarmCode);
            }

            UpdateConnectionStatus(true, "");
            UpdateAlarmData(alarmCode);
        }
    }

// ============================================
    // NEW POLLING LOGIC ADDED
    // ============================================
    private IEnumerator PollFeeders()
    {
        // This query fetches all minfeeder and bdfeeder fields in one go using Regex
        string fluxQuery = 
$@"from(bucket: ""{bucketName}"")
  |> range(start: {range})
  |> filter(fn: (r) => r._measurement =~ /minfeeder_[1-5]|bdfeeder_[1-5]/)
  |> filter(fn: (r) => r._field =~ /minfeeder_[1-5]|bdfeeder_[1-5]/)
  |> last()";

        string fluxEscaped = fluxQuery
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "")
            .Replace("\n", "\\n");

        string url = $"{influxUrl}/api/v2/query?orgID={orgId}";
        string jsonBody = "{\"query\":\"" + fluxEscaped + "\"}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Authorization", "Token " + apiToken);
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Accept", "application/csv");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                ParseFeederCsv(www.downloadHandler.text);
            }
        }
    }

    private void ParseFeederCsv(string csv)
    {
        if (string.IsNullOrEmpty(csv)) return;

        string[] lines = csv.Split('\n');
        int fieldIndex = -1;
        int valueIndex = -1;

        // Header parsing to find columns
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.StartsWith("#")) continue;

            // Find headers
            if (fieldIndex == -1) 
            {
                var headers = line.Split(',');
                for (int i = 0; i < headers.Length; i++)
                {
                    if (headers[i].Trim() == "_field") fieldIndex = i;
                    if (headers[i].Trim() == "_value") valueIndex = i;
                }
                continue;
            }

            // Parse data rows
            var cols = line.Split(',');
            if (cols.Length > fieldIndex && cols.Length > valueIndex)
            {
                string fieldName = cols[fieldIndex];
                string valStr = cols[valueIndex];
                int val = 0;
                int.TryParse(valStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out val);

                // Update MinFeeders
                if (fieldName.StartsWith("minfeeder_"))
                {
                    char numChar = fieldName[fieldName.Length - 1];
                    if (int.TryParse(numChar.ToString(), out int index) && index >= 1 && index <= 5)
                    {
                        MinFeederValues[index] = val;
                    }
                }
                // Update BdFeeders
                else if (fieldName.StartsWith("bdfeeder_"))
                {
                    char numChar = fieldName[fieldName.Length - 1];
                    if (int.TryParse(numChar.ToString(), out int index) && index >= 1 && index <= 5)
                    {
                        BdFeederValues[index] = val;
                    }
                }
            }
        }
    }

    private string BuildFluxQuery()
    {
        return
$@"from(bucket: ""{bucketName}"")
  |> range(start: {range})
  |> filter(fn: (r) => r._measurement == ""{measurement}"")
  |> filter(fn: (r) => r._field == ""{field}"")
  |> last()";
    }

    // ============================================
    // DATA UPDATE LOGIC
    // ============================================

    private void UpdateAlarmData(int alarmCode)
    {
        // Store previous values to detect changes
        int previousCode = CurrentAlarmCode;
        bool previousHasAlarm = HasActiveAlarm;

        // Update properties
        CurrentAlarmCode = alarmCode;
        HasActiveAlarm = alarmCode != 0;
        LastUpdateTime = System.DateTime.Now;

        // Get description
        if (!alarmDescriptions.TryGetValue(alarmCode, out string description))
        {
            description = $"Unknown alarm ({alarmCode})";
        }
        CurrentAlarmDescription = description;

        // Fire update event (always)
        AlarmUpdated?.Invoke();

        // Fire changed event (only if alarm actually changed)
        if (CurrentAlarmCode != previousCode || HasActiveAlarm != previousHasAlarm)
        {
            AlarmChanged?.Invoke(CurrentAlarmCode, CurrentAlarmDescription, HasActiveAlarm);
            Debug.Log($"[{bucketName}] Alarm changed: Code={CurrentAlarmCode}, Active={HasActiveAlarm}");
        }
    }

    private void UpdateConnectionStatus(bool connected, string error)
    {
        bool previousConnection = IsConnected;
        IsConnected = connected;
        LastError = error;

        // Fire event if connection status changed
        if (IsConnected != previousConnection)
        {
            ConnectionChanged?.Invoke(IsConnected, LastError);
        }
    }

    // ============================================
    // CSV PARSING
    // ============================================

    private string ExtractLastValueFromCsv(string csv)
    {
        if (string.IsNullOrEmpty(csv)) return null;

        string[] lines = csv.Split('\n');
        string headerLine = null;
        string lastDataLine = null;
        int valueIndex = -1;

        foreach (var raw in lines)
        {
            string line = raw.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.StartsWith("#")) continue;

            if (headerLine == null)
            {
                headerLine = line;
                var headers = headerLine.Split(',');
                for (int i = 0; i < headers.Length; i++)
                {
                    if (headers[i].Trim() == "_value")
                    {
                        valueIndex = i;
                        break;
                    }
                }
            }
            else
            {
                lastDataLine = line;
            }
        }

        if (lastDataLine == null || valueIndex < 0) return null;

        var cols = lastDataLine.Split(',');
        if (valueIndex >= cols.Length) return null;

        return cols[valueIndex];
    }

    // ============================================
    // PUBLIC HELPER METHODS
    // ============================================

    /// <summary>
    /// Get the description for any alarm code
    /// </summary>
    public string GetAlarmDescription(int code)
    {
        if (alarmDescriptions.TryGetValue(code, out string description))
            return description;
        return $"Unknown alarm ({code})";
    }

    /// <summary>
    /// Check if a specific alarm code is currently active
    /// </summary>
    public bool IsAlarmActive(int code)
    {
        return CurrentAlarmCode == code && HasActiveAlarm;
    }

    /// <summary>
    /// Get all available alarm codes and descriptions
    /// </summary>
    public Dictionary<int, string> GetAllAlarmCodes()
    {
        return new Dictionary<int, string>(alarmDescriptions);
    }

    /// <summary>
    /// Check if alarm is in a specific category
    /// </summary>
    public bool IsAnyAlarm()
    {
        return HasActiveAlarm;
    }

    public bool IsFeederAlarm()
    {
        return CurrentAlarmCode >= 1 && CurrentAlarmCode <= 5;
    }

    public bool IsHopperAlarm()
    {
        return (CurrentAlarmCode >= 9 && CurrentAlarmCode <= 11) ||
               (CurrentAlarmCode >= 15 && CurrentAlarmCode <= 17);
    }

    public bool IsDockAlarm()
    {
        return CurrentAlarmCode >= 59 && CurrentAlarmCode <= 65;
    }

    public bool IsEmergencyAlarm()
    {
        return CurrentAlarmCode == 58 || CurrentAlarmCode == 37;
    }

    public bool IsCriticalAlarm()
    {
        // Define which alarms are critical
        return CurrentAlarmCode == 37 || // Security area broken
               CurrentAlarmCode == 58 || // Emergency status
               (CurrentAlarmCode >= 45 && CurrentAlarmCode <= 56); // Equipment errors
    }

    /// <summary>
    /// Manual refresh - forces an immediate query
    /// </summary>
    public void ForceRefresh()
    {
        if (pollCoroutine != null)
        {
            StopCoroutine(pollCoroutine);
        }
        pollCoroutine = StartCoroutine(PollLoop());
    }
}