using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightChange_401 : MonoBehaviour
{
    public InfluxAlarmDataProvider_401 influxScript;

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
        bool bd_cyl_1 = influxScript.BdFeederValues[1];
        bool bd_box_2 = influxScript.BdFeederValues[2];
        bool bd_cyl_3 = influxScript.BdFeederValues[3];
        bool bd_box_4 = influxScript.BdFeederValues[4];
        bool bd_cyl_5 = influxScript.BdFeederValues[5];

        bool activeAlarm = influxScript.IsAnyAlarm();

        if (activeAlarm) ChangeMaterial(Red);
        else if (!bd_box_2 || !bd_box_4) ChangeMaterial(Yellow);
        else if (
            (!bd_cyl_1 && !bd_cyl_3) ||
            (!bd_cyl_1 && !bd_cyl_5) ||
            (!bd_cyl_3 && !bd_cyl_5)
        )
        {
            ChangeMaterial(Yellow);
        }
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