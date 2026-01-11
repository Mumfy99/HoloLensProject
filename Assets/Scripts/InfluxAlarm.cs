using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

public class InfluxAlarm : MonoBehaviour
{
    [Header("InfluxDB Settings")]
    public string influxUrl = "http://130:130:130:205:8086";
    public string orgId = "8d72c1fb56ba4914";
    public string apiToken = "CVU6Py8WeUXfkQcdpXXSFJg2MvvbV5PiPEfK2KEbK0HSssgG9QN4YbSVUl-Mo7T4_7wJGX6CL_ZB-_UAQI4Dmg==";

    [Header("Query Settings")]
    // SIF 401 → "Sif 401"
    // SIF 402 → "Sif 402"
    public string bucketName = "Sif 401";
    public string measurement = "alarm_code";
    public string field = "alarm";
    public string range = "-1h";

    [Header("UI")]
    // Whole alarm box you want to hide/show (e.g. Outer_AlarmTile_401 / 402)
    public GameObject alarmPanel;
    // Text that shows "0 Total" / "1 Total"
    public TMP_Text totalText;
    // Text for alarm description (optional)
    public TMP_Text descriptionText;

    [Header("Refresh")]
    public float refreshIntervalSeconds = 2f;

    private Coroutine pollCoroutine;

    // Alarm codes → descriptions
    private readonly Dictionary<int, string> alarms = new Dictionary<int, string>
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

    private void Start()
    {
        // IMPORTANT: this GameObject (AlarmController_401 / 402) must stay active.
        // Only alarmPanel is hidden/shown.
        pollCoroutine = StartCoroutine(PollLoop());
    }

    private void OnDestroy()
    {
        if (pollCoroutine != null)
            StopCoroutine(pollCoroutine);
    }

    private IEnumerator PollLoop()
    {
        while (true)
        {
            yield return PollOnce();
            yield return new WaitForSeconds(refreshIntervalSeconds);
        }
    }

    private IEnumerator PollOnce()
    {
        string url = $"{influxUrl}/api/v2/query?orgID={orgId}";
        string fluxQuery =
$@"from(bucket: ""{bucketName}"")
  |> range(start: {range})
  |> filter(fn: (r) => r._measurement == ""{measurement}"")
  |> filter(fn: (r) => r._field == ""{field}"")
  |> last()";

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
                Debug.LogError($"[Alarm {bucketName}] query error: {www.error} | {www.downloadHandler.text}");
                // On error, just show "no alarm" but keep the loop running
                UpdateUI(0);
                yield break;
            }

            string response = www.downloadHandler.text;
            Debug.Log($"[Alarm {bucketName}] InfluxDB response:\n{response}");

            string valueString = ExtractLastValueFromCsv(response);

            int alarmCode = 0;
            if (!string.IsNullOrEmpty(valueString))
                int.TryParse(valueString, NumberStyles.Integer, CultureInfo.InvariantCulture, out alarmCode);

            UpdateUI(alarmCode);
        }
    }

    private void UpdateUI(int alarmCode)
    {
        Debug.Log($"[Alarm {bucketName}] alarmCode = {alarmCode}");

        bool hasAlarm = alarmCode != 0;          // 0 = no alarm → hide

        if (alarmPanel != null)
        {
            alarmPanel.SetActive(hasAlarm);      // show only when there is an alarm
            Debug.Log($"[Alarm {bucketName}] SetActive({hasAlarm}) on {alarmPanel.name}");
        }

        if (totalText != null)
        {
            int total = hasAlarm ? 1 : 0;
            totalText.text = $"{total} Total";
        }

        if (descriptionText != null)
        {
            string desc;
            if (!alarms.TryGetValue(alarmCode, out desc))
                desc = $"Unknown alarm ({alarmCode})";

            descriptionText.text = hasAlarm ? desc : "No alarm";
        }
    }

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
}
