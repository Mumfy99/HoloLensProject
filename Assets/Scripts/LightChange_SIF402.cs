using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightChange_402 : MonoBehaviour
{
    public InfluxAlarmDataProvider_402 influxScript;

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

        bool activeAlarm = influxScript.IsAnyAlarm();

        if (activeAlarm) ChangeMaterial(Red);
        else ChangeMaterial(Green);
        
        // DEBUG KEY INPUTS
        // if (Input.GetKeyDown(KeyCode.Alpha0)) ChangeMaterial(Grey);
        // if (Input.GetKeyDown(KeyCode.Alpha1)) ChangeMaterial(Red);
        // if (Input.GetKeyDown(KeyCode.Alpha2)) ChangeMaterial(Green);
        // if (Input.GetKeyDown(KeyCode.Alpha3)) ChangeMaterial(Yellow);
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