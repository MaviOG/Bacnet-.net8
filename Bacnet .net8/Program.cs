﻿/**************************************************************************
*                           MIT License
* 
* Copyright (C) 2016 Frederic Chaxel <fchaxel@free.fr>
*
* Permission is hereby granted, free of charge, to any person obtaining
* a copy of this software and associated documentation files (the
* "Software"), to deal in the Software without restriction, including
* without limitation the rights to use, copy, modify, merge, publish,
* distribute, sublicense, and/or sell copies of the Software, and to
* permit persons to whom the Software is furnished to do so, subject to
* the following conditions:
*
* The above copyright notice and this permission notice shall be included
* in all copies or substantial portions of the Software.
*
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
* EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
* MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
* IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
* CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
* TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
* SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*
*********************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Xml;
using System.Threading;
using System.ServiceProcess;
using Microsoft.Win32;
using BaCSharp;
using AnotherStorageImplementation;
using System.IO.BACnet;
using IniParser;
using IniParser.Model;
using System.Globalization;
using System.Diagnostics;
using System.Timers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.IO.BACnet.Serialize;
using System.Collections;
using ModbusTCPClientV1;
using static System.Collections.Specialized.BitVector32;
using NModbus.Utility;
using System.Runtime.Serialization.Formatters;
using Ini;
using static System.Runtime.InteropServices.JavaScript.JSType;
using IniParser.Exceptions;

namespace Tal_to_Bacnet
{
    
    class Program
    {
        static void Main(string[] args)
        {
          
          
                List<BacnetActivity> bacnet = new List<BacnetActivity>();
                List<DeviceObject> devobj = new List<DeviceObject>();
                List<ModbusTCPClient> modbusClient = new List<ModbusTCPClient>();
                List<AnalogValue<float>> av = new List<AnalogValue<float>>();
                List<MultiStateOutput> mv = new List<MultiStateOutput>();
                List<BinaryOutput> dv = new List<BinaryOutput>();
                System.Timers.Timer timerUpdateTrend = new System.Timers.Timer(interval: 1000);
                bool TrendRun = false;
                int devices;
                uint priority_read;
                void Template(string FileName, IniFile MyIni)
                {
                    MyIni.ChangeFile(FileName + ".ini");//naredi za branje fila v datoteki template
                }
                void InitBacnetDictionary(IniFile MyIni, int device, int steviloTock, SQLite SQLiteClass)
                {
                    string local_endpoint_ip = MyIni.IniReadValue( "Settings", "local_endpoint_ip","");
                    int bac_port = Convert.ToInt32(MyIni.IniReadValue( "Settings", "bac_port",""));
                    uint Bacid = Convert.ToUInt32(MyIni.IniReadValue( "Device_" + (device + 1), "Bacnet_ID",""));
                    devobj.Add(new DeviceObject(Bacid, MyIni.IniReadValue( "Device_" + (device + 1), "Name",""), MyIni.IniReadValue( "Device_" + (device + 1), "Description","")));
                    string Section = "Device_" + (1 + device);
                    if (steviloTock == 0)
                    {
                        Section = MyIni.IniReadValue( "Device_" + (device + 1), "Template","");
                        Template(MyIni.IniReadValue( "Device_" + (device + 1), "Template",""), MyIni);
                        steviloTock = GetAnalogValueCount(MyIni, Section, "Point_");
                    }


                    for (int stevec = 0; stevec < steviloTock; stevec++)
                    {
                        string Point = MyIni.IniReadValue( Section, "Point_" + (stevec + 1),"");
                        string[] values = Point.Split(',');
                        string TypePointString = values[1].Substring(0, 2);
                        switch (TypePointString)
                        {
                            case "AV"://ANALOG VALUE
                                av.Add(new AnalogValue<float>(Convert.ToInt32(values[0]), values[2], values[3], (BacnetUnitsId)Convert.ToInt32(values[6])));
                                av.Last().m_PROP_LOW_LIMIT = 100;
                                av.Last().m_PROP_HIGH_LIMIT = 400;
                                BacnetBitString limitEnableBitString = new BacnetBitString();//
                                limitEnableBitString.SetBit(0, true);//
                                limitEnableBitString.SetBit(1, true);//
                                av.Last().m_PROP_LIMIT_ENABLE = limitEnableBitString;//
                                devobj.Last().AddBacnetObject(av.Last());
                                av.Last().m_PROP_NOTIFICATION_CLASS = 4;

                                SQLiteClass.InsertPoint(new AnalogValue<float>(Convert.ToInt32(values[0]), values[2], values[3], (BacnetUnitsId)Convert.ToInt32(values[6])), Bacid);
                                break;
                            case "DV"://DIGITAL VALUE
                                dv.Add(new BinaryOutput(Convert.ToInt32(values[0]), values[2], values[3]));
                                devobj.Last().AddBacnetObject(dv.Last());
                                SQLiteClass.InsertPoint(new BinaryOutput(Convert.ToInt32(values[0]), values[2], values[3]), Bacid);
                                break;
                            case "MV"://MULTISTAGE VALUE  
                                mv.Add(new MultiStateOutput(Convert.ToInt32(values[0]), values[2], values[3], Convert.ToUInt32(values[6])));
                                for (int i = 0; i < values.Length - 8; i++)
                                {
                                    mv[stevec].m_PROP_STATE_TEXT[i] = new BacnetValue(values[i + 8]);
                                }
                                devobj.Last().AddBacnetObject(mv.Last());
                                SQLiteClass.InsertPoint(mv.Last(), Bacid);
                                break;
                            default: throw new Exception("String not in right format");
                        }
               
                    }

                    devobj[device].Cli2Native();
                    bacnet.Add(new BacnetActivity(devobj, modbusClient));
                    bacnet[device].StartActivity(devobj[device], local_endpoint_ip, bac_port);
                }
        
        
                void read_modbus(IniFile MyIni, SQLite SQLiteClass)
                {
                    read_modbus_devices(MyIni, SQLiteClass);
                }
                void read_modbus_devices(IniFile MyIni, SQLite SQLiteClass)
                {

                    Dictionary<string, (int, int)> CoilStatusPoint = new Dictionary<string, (int, int)>();
                    Dictionary<string, (int, int)> InputStatusPoint = new Dictionary<string, (int, int)>();
                    Dictionary<string, (int, int)> HoldingRegiserPoint = new Dictionary<string, (int, int)>();
                    Dictionary<string, (int, int)> InputRegisterPoint = new Dictionary<string, (int, int)>();
                    Dictionary<string, (bool, int)> CoilStatusPointValue = new Dictionary<string, (bool, int)>();
                    Dictionary<string, (bool, int)> InputStatusPointValue = new Dictionary<string, (bool, int)>();
                    Dictionary<string, (float, int)> HoldingRegiserPointValue = new Dictionary<string, (float, int)>();
                    Dictionary<string, (float, int)> InputRegisterPointValue = new Dictionary<string, (float, int)>();
                    for (int device = 0; device < devices; device++)
                    {
                        CoilStatusPoint.Clear();
                        InputStatusPoint.Clear();
                        HoldingRegiserPoint.Clear();
                        InputRegisterPoint.Clear();
                        CoilStatusPointValue.Clear();
                        InputRegisterPointValue.Clear();
                        HoldingRegiserPointValue.Clear();
                        InputRegisterPointValue.Clear();
                        MyIni = new IniFile("Tal2Bacnet.ini");
                        SortPoints(device);
                        MyIni = new IniFile("Tal2Bacnet.ini");
                        ConnectionMbus(device);
                        Read_ModBus_values(device);
                        MyIni = new IniFile("Tal2Bacnet.ini");
                        write_virtual_modbus_device(device, SQLiteClass);
                        MyIni = new IniFile("Tal2Bacnet.ini");
                    }
                    void ConnectionMbus(int device)
                    {
                        modbusClient.Add(new ModbusTCPClient(MyIni.IniReadValue( "Device_" + (device + 1),"IP",""), Convert.ToInt32(MyIni.IniReadValue( "Device_" + (device + 1), "Port","")), Convert.ToByte(MyIni.IniReadValue( "Device_" + (device + 1), "ID",""))));
                    }
                    void SortPoints(int device)
                    {
                        int PointNumbers = GetAnalogValueCount(MyIni, "Device_" + (device + 1), "Point_");
                        string Section = "Device_" + (1 + device);
                        if (PointNumbers == 0)
                        {
                            Section = MyIni.IniReadValue( "Device_" + (device + 1), "Template","");
                            Template(MyIni.IniReadValue( "Device_" + (device + 1), "Template",""), MyIni);
                            PointNumbers = GetAnalogValueCount(MyIni, Section, "Point_");
                        }
                        for (int PointNumber = 1; PointNumber <= PointNumbers; PointNumber++)
                        {
                            string IniFileRow = MyIni.IniReadValue( Section, "Point_" + PointNumber,"");
                            string[] values = IniFileRow.Split(',');
                            if (values[4].Substring(0, 1) == "1")
                            {
                                int value1 = Convert.ToInt32(values[4].Substring(1));
                                int value2 = Convert.ToInt32(values[0]);
                                var tupleValue = (value1, value2);
                                CoilStatusPoint.Add("Point_" + PointNumber, tupleValue);
                            }
                            if (values[4].Substring(0, 1) == "2")
                            {
                                int value1 = Convert.ToInt32(values[4].Substring(1));
                                int value2 = Convert.ToInt32(values[0]);
                                var tupleValue = (value1, value2);
                                InputStatusPoint.Add("Point_" + PointNumber, tupleValue);
                            }
                            if (values[4].Substring(0, 1) == "3")
                            {
                                int value1 = Convert.ToInt32(values[4].Substring(1));
                                int value2 = Convert.ToInt32(values[0]);
                                var tupleValue = (value1, value2);
                                HoldingRegiserPoint.Add("Point_" + PointNumber, tupleValue);
                            }
                            if (values[4].Substring(0, 1) == "4")
                            {
                                int value1 = Convert.ToInt32(values[4].Substring(1));
                                int value2 = Convert.ToInt32(values[0]);
                                var tupleValue = (value1, value2);
                                InputRegisterPoint.Add("Point_" + PointNumber, tupleValue);
                            }
                        }
                    }
                    void Read_ModBus_values(int device)
                    {
                        if (CoilStatusPoint.Count != 0)
                        {
                            int startingAddress = 0;
                            bool[] readCoilRegistersBool = { };
                            int maxvalue = 0;
                            int maxModbusRead = Convert.ToInt32(MyIni.IniReadValue("Device_" + (device + 1), "MaxModbusRead", ""));
                            int ID = Convert.ToByte(MyIni.IniReadValue("Device_" + (device + 1), "ID", ""));
                            foreach (var value in CoilStatusPoint.Values)
                            {
                                if (value.Item1 > maxvalue)
                                {
                                    maxvalue = value.Item1;
                                }
                            }
                            try
                            {
                                for (int addresesToRead = maxModbusRead; (startingAddress + addresesToRead) <= maxvalue + maxModbusRead; startingAddress += maxModbusRead)
                                {

                                    bool[] data = modbusClient[device].ReadCoils((ushort)startingAddress, (ushort)maxModbusRead).Select(ushortValue => (bool)ushortValue).ToArray();
                                    readCoilRegistersBool = readCoilRegistersBool.Concat(data).ToArray();
                                }
                                foreach (KeyValuePair<string, (int, int)> kvp in CoilStatusPoint)
                                {
                                    string key = kvp.Key;
                                    int point = kvp.Value.Item1;
                                    point = point - 1;
                                    for (int sprehod = 0; sprehod < readCoilRegistersBool.Length; sprehod++)
                                    {
                                        if (sprehod == point)
                                        {
                                            string registerbit = MyIni.IniReadValue("Device_" + (device + 1), key, "");
                                            if (registerbit == "")
                                            {
                                                string Section = MyIni.IniReadValue("Device_" + (device + 1), "Template", "");
                                                Template(MyIni.IniReadValue("Device_" + (device + 1), "Template", ""), MyIni);
                                                registerbit = MyIni.IniReadValue(Section, key, "");
                                            }
                                            string[] array = registerbit.Split(',');
                                            registerbit = array[5];
                                            switch (registerbit)
                                            {
                                                case "I":
                                                    var value1 = readCoilRegistersBool[sprehod];
                                                    var value2 = kvp.Value.Item2;
                                                    var tupleValue = (value1, value2);
                                                    CoilStatusPointValue.Add(key, tupleValue);//v  bool vrednost 
                                                    goto here;
                                                case "U":
                                                    break;
                                                default:
                                                    throw new Exception("Error in ini file");
                                            }
                                        }
                                    }
                                here:;
                                }
                            }
                            catch (Exception) { }
                        }
                        MyIni = new IniFile("Tal2Bacnet.ini");
                        if (InputStatusPoint.Count != 0)
                        {
                            bool[] readDiscreteInputsRegistersBool = { };
                            int startingAddress = 0;
                            int maxvalue = 0;
                            int maxModbusRead = Convert.ToInt32(MyIni.IniReadValue("Device_" + (device + 1), "MaxModbusRead", ""));
                            foreach (var value in InputStatusPoint.Values)
                            {
                                if (value.Item1 > maxvalue)
                                {
                                    maxvalue = value.Item1;
                                }
                            }
                            for (int addresesToRead = maxModbusRead; (startingAddress + addresesToRead) <= maxvalue + maxModbusRead; startingAddress += maxModbusRead)
                            {
                                bool[] data = modbusClient[device].ReadDiscreteInputs((ushort)startingAddress, (ushort)maxModbusRead).Select(ushortValue => (bool)ushortValue).ToArray();
                                readDiscreteInputsRegistersBool = readDiscreteInputsRegistersBool.Concat(data).ToArray();
                            }
                            foreach (KeyValuePair<string, (int, int)> kvp in InputStatusPoint)
                            {
                                string key = kvp.Key;
                                int point = kvp.Value.Item1;
                                point = point - 1;
                                for (int sprehod = 0; sprehod < readDiscreteInputsRegistersBool.Length; sprehod++)
                                {
                                    if (sprehod == point)
                                    {
                                        string registerbit = MyIni.IniReadValue("Device_" + (device + 1), key, "");
                                        if (registerbit == "")
                                        {
                                            string Section = MyIni.IniReadValue("Device_" + (device + 1), "Template", "");
                                            Template(MyIni.IniReadValue("Device_" + (device + 1), "Template", ""), MyIni);
                                            registerbit = MyIni.IniReadValue(Section, key, "");
                                        }
                                        string[] array = registerbit.Split(',');
                                        registerbit = array[5];
                                        switch (registerbit)
                                        {
                                            case "I":
                                                var value1 = readDiscreteInputsRegistersBool[sprehod];
                                                var value2 = kvp.Value.Item2;
                                                var tupleValue = (value1, value2);
                                                InputStatusPointValue.Add(key, tupleValue);//v  bool vrednost 
                                                goto here;

                                            default:
                                                throw new Exception("Error in ini file");
                                        }
                                    }
                                }
                            here:;
                            }
                        }
                        MyIni = new IniFile("Tal2Bacnet.ini");
                        if (HoldingRegiserPoint.Count != 0)
                        {
                            int startingAddress = 0;
                            int maxvalue = 0;
                            int maxModbusRead = Convert.ToInt32(MyIni.IniReadValue("Device_" + (device + 1), "MaxModbusRead", ""));
                            int[] readHoldingRegistersInt = { };
                            foreach (var value in HoldingRegiserPoint.Values)
                            {
                                if (value.Item1 > maxvalue)
                                {
                                    maxvalue = value.Item1;
                                }
                            }
                            for (int addresesToRead = maxModbusRead; (startingAddress + addresesToRead) <= maxvalue + maxModbusRead; startingAddress += maxModbusRead)
                            {
                                try
                                {
                                    int[] data = modbusClient[device].ReadHoldingRegisters((ushort)startingAddress, (ushort)maxModbusRead).Select(ushortValue => (int)ushortValue).ToArray();
                                    readHoldingRegistersInt = readHoldingRegistersInt.Concat(data).ToArray();
                                }
                                catch (Exception) { }
                            }
                            foreach (KeyValuePair<string, (int, int)> kvp in HoldingRegiserPoint)
                            {
                                string key = kvp.Key;
                                int point = kvp.Value.Item1;
                                point = point - 1;
                                for (int sprehod = 0; sprehod < readHoldingRegistersInt.Length; sprehod++)
                                {
                                    if (sprehod == point)
                                    {

                                        string registerbit = MyIni.IniReadValue("Device_" + (device + 1), key, "");
                                        if (registerbit == "")
                                        {
                                            string Section = MyIni.IniReadValue("Device_" + (device + 1), "Template", "");
                                            Template(MyIni.IniReadValue("Device_" + (device + 1), "Template", ""), MyIni);
                                            registerbit = MyIni.IniReadValue(Section, key, "");
                                        }
                                        string[] array = registerbit.Split(',');
                                        registerbit = array[5];
                                        float times = float.Parse(array[7], CultureInfo.InvariantCulture);
                                        switch (registerbit)
                                        {
                                            case "I"://int
                                                var value1 = readHoldingRegistersInt[sprehod] * times;
                                                var value2 = kvp.Value.Item2;
                                                var tupleValue = (value1, value2);
                                                HoldingRegiserPointValue.Add(key, tupleValue);
                                                goto here;
                                            case "U"://uint
                                                var value5 = readHoldingRegistersInt[sprehod] * times;
                                                var value6 = kvp.Value.Item2;
                                                var tupleValueU = (value5, value6);
                                                HoldingRegiserPointValue.Add(key, tupleValueU);
                                                goto here;
                                            case "F"://Float
                                                var value3 = ConvertToFloat(readHoldingRegistersInt, Convert.ToUInt32(point)) * times;//Spremenit na ModBusUtility
                                                var value4 = kvp.Value.Item2;
                                                var tupleValueH = (value3, value4);
                                                HoldingRegiserPointValue.Add(key, tupleValueH);
                                                goto here;
                                            case "FS"://floatReverse
                                                var value7 = ConvertToFloatSwap(readHoldingRegistersInt, Convert.ToUInt32(point)) * times;
                                                var value8 = kvp.Value.Item2;
                                                var tupleValueHR = (value7, value8);
                                                HoldingRegiserPointValue.Add(key, tupleValueHR);
                                                goto here;
                                            case "W"://W
                                                break;
                                            default:
                                                throw new Exception("Error in ini file");
                                        }
                                    }
                                }
                            here:;
                            }
                        }
                        MyIni = new IniFile("Tal2Bacnet.ini");
                        if (InputRegisterPoint.Count != 0)
                        {
                            int[] readInputRegistersInt = { };
                            int startingAddress = 1;
                            int maxvalue = 0;
                            int maxModbusRead = Convert.ToInt32(MyIni.IniReadValue("Device_" + (device + 1), "MaxModbusRead", ""));
                            foreach (var value in InputRegisterPoint.Values)
                            {
                                if (value.Item1 > maxvalue)
                                {
                                    maxvalue = value.Item1;
                                }
                            }
                            for (int addresesToRead = maxModbusRead; (startingAddress + addresesToRead) <= maxvalue + maxModbusRead; startingAddress += maxModbusRead)
                            {
                                int[] data = modbusClient[device].ReadInputRegisters((ushort)startingAddress, (ushort)maxModbusRead).Select(ushortValue => (int)ushortValue).ToArray();
                                readInputRegistersInt = readInputRegistersInt.Concat(data).ToArray();
                            }
                            foreach (KeyValuePair<string, (int, int)> kvp in InputRegisterPoint)
                            {
                                string key = kvp.Key;
                                int point = kvp.Value.Item1;
                                point = point - 1;
                                for (int sprehod = 0; sprehod < readInputRegistersInt.Length; sprehod++)
                                {
                                    if (sprehod == point)
                                    {
                                        string registerbit = MyIni.IniReadValue("Device_" + (device + 1), key, "");
                                        if (registerbit == "")
                                        {
                                            string Section = MyIni.IniReadValue("Device_" + (device + 1), "Template", "");
                                            Template(MyIni.IniReadValue("Device_" + (device + 1), "Template", ""), MyIni);
                                            registerbit = MyIni.IniReadValue(Section, key, "");
                                        }
                                        string[] array = registerbit.Split(',');
                                        registerbit = array[5];
                                        float times = float.Parse(array[7], CultureInfo.InvariantCulture);
                                        switch (registerbit)
                                        {
                                            case "I"://int
                                                var value1 = readInputRegistersInt[sprehod] * times;
                                                var value2 = kvp.Value.Item2;
                                                var tupleValue = (value1, value2);
                                                InputRegisterPointValue.Add(key, tupleValue);
                                                goto here;
                                            case "U"://uint
                                                var value8 = Convert.ToUInt16(readInputRegistersInt[sprehod]) * times;
                                                var value9 = kvp.Value.Item2;
                                                var tupleValueU = (value8, value9);
                                                InputRegisterPointValue.Add(key, tupleValueU);
                                                goto here;
                                            case "F"://Float
                                                var value3 = ConvertToFloat(readInputRegistersInt, Convert.ToUInt32(point)) * times;
                                                var value4 = kvp.Value.Item2;
                                                var tupleValueH = (value3, value4);
                                                InputRegisterPointValue.Add(key, tupleValueH);
                                                goto here;
                                            case "FR"://float reverse
                                                var value6 = ConvertToFloatSwap(readInputRegistersInt, Convert.ToUInt32(point)) * times;
                                                var value7 = kvp.Value.Item2;
                                                var tupleValueHR = (value6, value7);
                                                InputRegisterPointValue.Add(key, tupleValueHR);
                                                goto here;
                                            case "W"://W
                                                break;
                                            default:
                                                throw new Exception("Error in ini file");
                                        }
                                    }
                                }
                            here:;
                            }
                        }
                    }
                    void write_virtual_modbus_device(int device, SQLite SQLiteClassWrite)
                    {
                        int BacID = Convert.ToInt32(MyIni.IniReadValue("Device_" + (device + 1), "Bacnet_ID", ""));



                        foreach (KeyValuePair<string, (bool, int)> kvp in CoilStatusPointValue)
                        {
                            string Point = MyIni.IniReadValue("Device_" + (device + 1), kvp.Key, "");
                            if (Point == "")
                            {
                                string Section = MyIni.IniReadValue("Device_" + (device + 1), "Template", "");
                                Template(MyIni.IniReadValue("Device_" + (device + 1), "Template", ""), MyIni);
                                Point = MyIni.IniReadValue(Section, kvp.Key, "");

                            }
                            string[] values = Point.Split(',');
                            try
                            {
                                var Point_ = (MultiStateOutput)devobj[device].ObjectsList.FirstOrDefault(obj => obj.m_PROP_OBJECT_IDENTIFIER.instance == kvp.Value.Item2);
                                Point_.internal_PROP_PRESENT_VALUE = Convert.ToUInt32(kvp.Value.Item1);
                                SQLiteClassWrite.PointUpdate(BacID, Point_);
                                UpdateTrendLog(BacID, Point_, kvp.Value.Item1);

                            }
                            catch (Exception)
                            {
                                try
                                {
                                    var Point_ = (BinaryOutput)devobj[device].ObjectsList.FirstOrDefault(obj => obj.m_PROP_OBJECT_IDENTIFIER.instance == kvp.Value.Item2);
                                    Point_.internal_PROP_PRESENT_VALUE = Convert.ToUInt32(kvp.Value.Item1);
                                    SQLiteClassWrite.PointUpdate(BacID, Point_);
                                    UpdateTrendLog(BacID, Point_, kvp.Value.Item1);

                                }
                                catch (Exception)
                                {
                                    try
                                    {
                                        var Point_ = (AnalogValue<float>)devobj[device].ObjectsList.FirstOrDefault(obj => obj.m_PROP_OBJECT_IDENTIFIER.instance == kvp.Value.Item2);
                                        Point_.internal_PROP_PRESENT_VALUE = Convert.ToUInt32(kvp.Value.Item1);
                                        SQLiteClassWrite.PointUpdate(BacID, Point_);

                                        UpdateTrendLog(BacID, Point_, kvp.Value.Item1);

                                    }
                                    catch (Exception)
                                    {
                                    }
                                }


                            }

                        }
                        MyIni = new IniFile("Tal2Bacnet.ini");
                        foreach (KeyValuePair<string, (bool, int)> kvp in InputStatusPointValue)
                        {
                            string Point = MyIni.IniReadValue("Device_" + (device + 1), kvp.Key, "");
                            if (Point == "")
                            {
                                string Section = MyIni.IniReadValue("Device_" + (device + 1), "Template", "");
                                Template(MyIni.IniReadValue("Device_" + (device + 1), "Template", ""), MyIni);
                                Point = MyIni.IniReadValue(Section, kvp.Key, "");
                            }
                            string[] values = Point.Split(',');
                            try
                            {
                                var Point_ = (MultiStateOutput)devobj[device].ObjectsList.FirstOrDefault(obj => obj.m_PROP_OBJECT_IDENTIFIER.instance == kvp.Value.Item2);
                                Point_.internal_PROP_PRESENT_VALUE = Convert.ToUInt32(kvp.Value.Item1);
                                SQLiteClassWrite.PointUpdate(BacID, Point_);
                                UpdateTrendLog(BacID, Point_, kvp.Value.Item1);



                            }
                            catch (Exception)
                            {
                                try
                                {
                                    var Point_ = (BinaryOutput)devobj[device].ObjectsList.FirstOrDefault(obj => obj.m_PROP_OBJECT_IDENTIFIER.instance == kvp.Value.Item2);
                                    Point_.internal_PROP_PRESENT_VALUE = Convert.ToUInt32(kvp.Value.Item1);
                                    SQLiteClassWrite.PointUpdate(BacID, Point_);
                                    UpdateTrendLog(BacID, Point_, kvp.Value.Item1);



                                }
                                catch (Exception)
                                {
                                    try
                                    {
                                        var Point_ = (AnalogValue<float>)devobj[device].ObjectsList.FirstOrDefault(obj => obj.m_PROP_OBJECT_IDENTIFIER.instance == kvp.Value.Item2);
                                        Point_.internal_PROP_PRESENT_VALUE = Convert.ToUInt32(kvp.Value.Item1);
                                        SQLiteClassWrite.PointUpdate(BacID, Point_);
                                        UpdateTrendLog(BacID, Point_, kvp.Value.Item1);



                                    }
                                    catch (Exception)
                                    {
                                    }
                                }
                            }
                        }
                        MyIni = new IniFile("Tal2Bacnet.ini");
                        foreach (KeyValuePair<string, (float, int)> kvp in HoldingRegiserPointValue)
                        {
                            string Point = MyIni.IniReadValue("Device_" + (device + 1), kvp.Key, "");
                            if (Point == "")
                            {
                                string Section = MyIni.IniReadValue("Device_" + (device + 1), "Template", "");
                                Template(MyIni.IniReadValue("Device_" + (device + 1), "Template", ""), MyIni);
                                Point = MyIni.IniReadValue(Section, kvp.Key, "");
                            }
                            string[] values = Point.Split(',');

                            try
                            {
                                var Point_ = (MultiStateOutput)devobj[device].ObjectsList.FirstOrDefault(obj => obj.m_PROP_OBJECT_IDENTIFIER.instance == kvp.Value.Item2);
                                Point_.internal_PROP_PRESENT_VALUE = Convert.ToUInt32(kvp.Value.Item1);
                                SQLiteClassWrite.PointUpdate(BacID, Point_);
                                UpdateTrendLog(BacID, Point_, kvp.Value.Item1);

                            }
                            catch (Exception)
                            {
                                try
                                {
                                    var Point_ = (BinaryOutput)devobj[device].ObjectsList.FirstOrDefault(obj => obj.m_PROP_OBJECT_IDENTIFIER.instance == kvp.Value.Item2);
                                    Point_.internal_PROP_PRESENT_VALUE = Convert.ToUInt32(kvp.Value.Item1);
                                    SQLiteClassWrite.PointUpdate(BacID, Point_);
                                    UpdateTrendLog(BacID, Point_, kvp.Value.Item1);


                                }
                                catch (Exception)
                                {
                                    try
                                    {
                                        var Point_ = (AnalogValue<float>)devobj[device].ObjectsList.FirstOrDefault(obj => obj.m_PROP_OBJECT_IDENTIFIER.instance == kvp.Value.Item2);
                                        Point_.internal_PROP_PRESENT_VALUE = kvp.Value.Item1;
                                        SQLiteClassWrite.PointUpdate(BacID, Point_);
                                        UpdateTrendLog(BacID, Point_, kvp.Value.Item1);




                                    }
                                    catch (Exception)
                                    {

                                    }
                                }
                            }



                        }
                        MyIni = new IniFile("Tal2Bacnet.ini");
                        foreach (KeyValuePair<string, (float, int)> kvp in InputRegisterPointValue)
                        {

                            string Point = MyIni.IniReadValue("Device_" + (device + 1), kvp.Key, "");
                            if (Point == "")
                            {
                                string Section = MyIni.IniReadValue("Device_" + (device + 1), "Template", "");
                                Template(MyIni.IniReadValue("Device_" + (device + 1), "Template", ""), MyIni);
                                Point = MyIni.IniReadValue(Section, kvp.Key, "");
                            }
                            string[] values = Point.Split(',');
                            try
                            {
                                var Point_ = (MultiStateOutput)devobj[device].ObjectsList.FirstOrDefault(obj => obj.m_PROP_OBJECT_IDENTIFIER.instance == kvp.Value.Item2);
                                Point_.internal_PROP_PRESENT_VALUE = Convert.ToUInt32(kvp.Value.Item1);
                                SQLiteClassWrite.PointUpdate(BacID, Point_);
                                UpdateTrendLog(BacID, Point_, kvp.Value.Item1);


                            }
                            catch (Exception)
                            {
                                try
                                {
                                    var Point_ = (BinaryOutput)devobj[device].ObjectsList.FirstOrDefault(obj => obj.m_PROP_OBJECT_IDENTIFIER.instance == kvp.Value.Item2);
                                    Point_.internal_PROP_PRESENT_VALUE = Convert.ToUInt32(kvp.Value.Item1);
                                    SQLiteClassWrite.PointUpdate(BacID, Point_);
                                    UpdateTrendLog(BacID, Point_, kvp.Value.Item1);

                                }
                                catch (Exception)
                                {
                                    try
                                    {
                                        var Point_ = (AnalogValue<float>)devobj[device].ObjectsList.FirstOrDefault(obj => obj.m_PROP_OBJECT_IDENTIFIER.instance == kvp.Value.Item2);


                                        Point_.internal_PROP_PRESENT_VALUE = kvp.Value.Item1;
                                        SQLiteClassWrite.PointUpdate(BacID, Point_);
                                        UpdateTrendLog(BacID, Point_, kvp.Value.Item1);

                                    }
                                    catch (Exception)
                                    {
                                    }
                                }


                            }

                        }


                    }
                    
                }
                float ConvertRegistersToFloat_(int lowRegister, int highRegister)
                {
                    byte[] highRegisterBytes = BitConverter.GetBytes(highRegister);
                    byte[] lowRegisterBytes = BitConverter.GetBytes(lowRegister);
                    byte[] floatBytes = {
                                            lowRegisterBytes[0],
                                            lowRegisterBytes[1],
                                            highRegisterBytes[0],
                                            highRegisterBytes[1]
                                        };
                    return BitConverter.ToSingle(floatBytes, 0);
                }
                float ConvertToFloat(int[] registers, uint start)
                {
                    ushort[] temp = new ushort[2];

                    temp[1] = (ushort)registers[start];
                    temp[0] = (ushort)registers[start + 1];
                    return Modbus.Utility.ModbusUtility.GetSingle(temp[0], temp[1]);

                    //return EasyModbus.ModbusClient.ConvertRegistersToFloat(temp); 
                }//Zamenjat z modbuuUtility
                float ConvertToFloatSwap(int[] registers, uint start)
                {
                    ushort[] temp = new ushort[2];
                    start = start * 2;
                    temp[1] = (ushort)registers[start + 1];
                    temp[0] = (ushort)registers[start];
                    return Modbus.Utility.ModbusUtility.GetSingle(temp[1], temp[0]);
                    //return EasyModbus.ModbusClient.ConvertRegistersToFloat(temp);
                }//Zamenjat z modbuuUtility
                void WorkingLoop()
                {
                   
                    string DataBasePath = System.IO.Path.Combine(System.IO.Directory.GetParent(System.IO.Directory.GetParent(Environment.CurrentDirectory).FullName).FullName, "SQLite", "DataBase.db");
                    string connectionString = $"Data Source={DataBasePath};Version=3;";
                    var MyIni = new IniFile("Tal2Bacnet.ini");
                    devices = Convert.ToInt32(MyIni.IniReadValue( "Settings", "Devices",""));
                    SQLite SQLiteClass = new SQLite(DataBasePath, MyIni, devices, connectionString);
                    for (int device = 0; device < devices; device++)
                    {
                        MyIni = new IniFile("Tal2Bacnet.ini");
                        int steviloTock = GetAnalogValueCount(MyIni, "Device_" + (device + 1), "Point_");
                        InitBacnetDictionary(MyIni, device, steviloTock, SQLiteClass);
                    }
                    for (; ; )
                    {

                        // NextUpdatetime.m_PresentValue = DateTime.Now.AddMinutes(UpdateDelay);
                        Console.WriteLine("Refreshing....");

                        read_modbus(MyIni, SQLiteClass);
                        Console.WriteLine("Done....");


                        // Wait 10 minutes or a stop condition
                        
                            return;
                    }
                }
        
                 int GetAnalogValueCount(IniFile link, string Key, string Section)
                {
                    var parser = new FileIniDataParser();

                    IniData data = parser.ReadFile(link.Path);

                    var analogValueKeys = data[Key].Select(k => k.KeyName).Where(k => k.StartsWith(Section));

                    return analogValueKeys.Count();
                }
                 int GetAnalogValueCountKey(IniFile link, string sectionString)
                {
                    IniFile parser = new IniFile(link.path);
                IniData data = ReadIniFile(link.path);//Uprasanje ce deluje kot mora
                    int sectionCount = data.Sections.Count(section => section.SectionName.StartsWith(sectionString));
                    return sectionCount;
                }
           
            

        }
    }
}
    
