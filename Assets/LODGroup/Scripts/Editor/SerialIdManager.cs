using System;
using UnityEngine;
public class SerialIdManager
{
    public static readonly SerialIdManager Instance = new SerialIdManager();

    private int _serialNumber = 0;
    private string _lastTime = "";

    public String GetSid()
    {
        string curId = DateTime.Now.ToString();
        if(curId == _lastTime)//相同时间
        {
            _lastTime = curId;
            _serialNumber++;
            return curId + _serialNumber;
        }
        else//不同时间
        {
            _serialNumber = 0;
            _lastTime = curId;
            return curId + _serialNumber;
        }
    }
}
