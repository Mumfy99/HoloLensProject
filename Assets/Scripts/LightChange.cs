using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightChange : MonoBehaviour
{
    public InfluxAlarmDataProvider alarmDataScript;

    public Renderer bulbPart; 

    // Define the Materials we want to use
    public Material Grey;
    public Material Green;
    public Material Yellow;
    public Material Red;

    void Start()
    {
        ChangeMaterial(Grey);
    }

    void Update()
    {
        // if (alarmDataScript == null) return;

        bool activeAlarm = alarmDataScript.IsAnyAlarm();
        // if (Input.GetKeyDown(KeyCode.Alpha2)) ChangeMaterial(Green);
        // if (Input.GetKeyDown(KeyCode.Alpha3)) ChangeMaterial(Yellow);

        if (activeAlarm) ChangeMaterial(Red);
        // else if () ChangeMaterial(Yellow);
        else ChangeMaterial(Grey);
    }

    void ChangeMaterial(Material newMat)
    {
        // SAFETY CHECKS
        if (bulbPart == null || newMat == null) 
        {
            Debug.LogError("Forgot to attach the bulb or material");
            return;
        }
        else if (bulbPart.material != newMat)
        {
            bulbPart.material = newMat;
        }

    }
}