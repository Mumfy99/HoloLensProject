using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

public class InfluxD : MonoBehaviour
{
    [Header("InfluxDB Settings")]
    public string influxUrl = "http://130.130.130.205:8086";
    public string orgId = "8d72c1fb56ba4914";
    public string apiToken = "CVU6Py8WeUXfkQcdpXXSFJg2MvvbV5PiPEfK2KEbK0HSssgG9QN4YbSVUl-Mo7T4_7wJGX6CL_ZB-_UAQI4Dmg==";

    [Header("Query Settings")]
    // For SIF 401 objects: "Sif 401"
    // For SIF 402 objects: "Sif 402"
    public string bucketName = "Sif 401";

    // Voltage tiles: "Voltage"
    // Runtime tiles: "RunningTime"
    public string measurement = "Voltage";

    // Voltage tiles: "voltage"
    // Runtime tiles: "running_time"
    public string field = "voltage";

    public string range = "-1h";

    [Header("UI")]
    public TMP_Text valueText;

    [Header("Display Settings")]
    public bool valueIsNumber = true;

    // If true, treat numeric value as seconds and show HH:MM
    public bool displayAsDuration = false;

    public string unitSuffix = " V"; // e.g. " V" for voltage, " s" for seconds
    public float refreshIntervalSeconds = 5f;

    private void Awake()
    {
        if (valueText == null)
            valueText = GetComponent<TMP_Text>();
    }

    private void Start()
    {
        StartCoroutine(QueryLoop());
    }

    private IEnumerator QueryLoop()
    {
        while (true)
        {
            yield return QueryInfluxOnce();
            yield return new WaitForSeconds(refreshIntervalSeconds);
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

    private IEnumerator QueryInfluxOnce()
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
                Debug.LogError("InfluxDB query error: " + www.error + " | " + www.downloadHandler.text);
                if (valueText != null) valueText.text = "Error";
                yield break;
            }

            string response = www.downloadHandler.text;
            Debug.Log("InfluxDB response:\n" + response);

            string valueString = ExtractLastValueFromCsv(response);

            if (valueString == null)
            {
                Debug.LogWarning("No _value column found in CSV");
                if (valueText != null) valueText.text = "N/A";
                yield break;
            }

            if (valueText == null)
                yield break;

            if (valueIsNumber)
            {
                if (float.TryParse(valueString, NumberStyles.Float, CultureInfo.InvariantCulture, out float number))
                {
                    if (displayAsDuration)
                    {
                        int totalSeconds = Mathf.RoundToInt(number);
                        int hours = totalSeconds / 3600;
                        int minutes = (totalSeconds % 3600) / 60;
                        valueText.text = $"{hours:D2}:{minutes:D2}" + unitSuffix;
                    }
                    else
                    {
                        valueText.text = number.ToString("0.0") + unitSuffix;
                    }
                }
                else
                {
                    Debug.LogWarning("Failed to parse value as number: " + valueString);
                    valueText.text = valueString + unitSuffix;
                }
            }
            else
            {
                valueText.text = valueString + unitSuffix;
            }
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

