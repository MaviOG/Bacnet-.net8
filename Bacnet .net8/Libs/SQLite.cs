using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using BaCSharp;
using System.IO.BACnet;
using Ini;
namespace Tal_to_Bacnet
{
    internal class SQLite
    {
        public string FilePath { get; set; }
        public IniFile Myini { get; set; }
        public int device { get; set; }
        public string ConnectionString { get; set; }
        public SQLite(string filePath,IniFile myIni, int device, string connectionString)
        {
            this.FilePath = filePath;
            this.Myini = myIni;
            this.device = device;
            this.ConnectionString = connectionString;
            File.Create(this.FilePath);
            string TableName = Myini.IniReadValue("Device_" + device + 1, "Name", "");    
            using (SqliteConnection connection = new SqliteConnection(this.ConnectionString))
            {
                connection.Open();
                using (SqliteCommand command = new SqliteCommand(
                "CREATE TABLE IF NOT EXISTS AnalogValue(DB_ID INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,BacNetID INTEGER,ObjID INTEGER,Name STRING,Description STRING,Unit INTEGER,PresentValue REAL);", connection))
                { command.ExecuteNonQuery(); }
                using (SqliteCommand command = new SqliteCommand(
                "CREATE TABLE IF NOT EXISTS DigitalValue(DB_ID INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,BacNetID INTEGER,ObjID INTEGER,Name STRING,Description STRING,PresentValue INTEGER);", connection))
                { command.ExecuteNonQuery(); }
                using (SqliteCommand command = new SqliteCommand(
                "CREATE TABLE IF NOT EXISTS MultistateValue(DB_ID INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,BacNetID INTEGER,ObjID INTEGER,Name STRING,Description STRING,NumberOfStates INTEGER,NumberOfStatesName STRING,PresentValue INTEGER);", connection))
                { command.ExecuteNonQuery(); }
                connection.Close();
            }
        }
        public void InsertPoint(AnalogValue<float> AnalogValue,uint Bacid)
        {
            using (SqliteConnection connection = new SqliteConnection(this.ConnectionString))
            {
                connection.Open();
                using (SqliteCommand command = new SqliteCommand(
                    "INSERT INTO AnalogValue(BacNetID, ObjID, Name, Description, Unit, PresentValue) " +
                    "VALUES (@BacNetID, @ObjID, @Name, @Description, @Unit, @PresentValue);", connection))
                {
                    command.Parameters.AddWithValue("@BacNetID", Bacid);
                    command.Parameters.AddWithValue("@ObjID", AnalogValue.m_PROP_OBJECT_IDENTIFIER.instance);
                    command.Parameters.AddWithValue("@Name", AnalogValue.m_PROP_OBJECT_NAME);
                    command.Parameters.AddWithValue("@Description", AnalogValue.m_PROP_DESCRIPTION);
                    command.Parameters.AddWithValue("@Unit", AnalogValue.m_PROP_UNITS);
                    command.Parameters.AddWithValue("@PresentValue", AnalogValue.internal_PROP_PRESENT_VALUE);
                    command.ExecuteNonQuery();
                }
                connection.Close();
            }
        }
        public void InsertPoint(BinaryOutput DigitalValue,uint Bacid)
        {
            using (SqliteConnection connection = new SqliteConnection(this.ConnectionString))
            {
                connection.Open();
                using (SqliteCommand command = new SqliteCommand(
                    "INSERT INTO DigitalValue (BacNetID, ObjID, Name, Description, PresentValue) " +
                    "VALUES (@BacNetID, @ObjID, @Name, @Description, @PresentValue);", connection))
                {
                    command.Parameters.AddWithValue("@BacNetID", Bacid);
                    command.Parameters.AddWithValue("@ObjID", DigitalValue.m_PROP_OBJECT_IDENTIFIER.instance);
                    command.Parameters.AddWithValue("@Name", DigitalValue.m_PROP_OBJECT_NAME);
                    command.Parameters.AddWithValue("@Description", DigitalValue.m_PROP_DESCRIPTION);
                    command.Parameters.AddWithValue("@PresentValue", DigitalValue.internal_PROP_PRESENT_VALUE);
                    command.ExecuteNonQuery();
                }
                connection.Close();
            }
        }
        public void InsertPoint(MultiStateOutput MultistateValue, uint Bacid)
        {
            string NameState = "";
            foreach(BacnetValue Name in MultistateValue.m_PROP_STATE_TEXT)
            {
                NameState += Name.Value+",";
            }
            using (SqliteConnection connection = new SqliteConnection(this.ConnectionString))
            {
                connection.Open();
                using (SqliteCommand command = new SqliteCommand(
                    "INSERT INTO MultistateValue(BacNetID, ObjID, Name, Description,NumberOfStates,NumberOfStatesName,PresentValue) " +
                    "VALUES (@BacNetID, @ObjID, @Name, @Description,@NumberOfStates,@NumberOfStatesName,@PresentValue);", connection))
                {
                    command.Parameters.AddWithValue("@BacNetID", Bacid);
                    command.Parameters.AddWithValue("@ObjID", MultistateValue.m_PROP_OBJECT_IDENTIFIER.instance);
                    command.Parameters.AddWithValue("@Name", MultistateValue.m_PROP_OBJECT_NAME);
                    command.Parameters.AddWithValue("@Description", MultistateValue.m_PROP_DESCRIPTION);
                    command.Parameters.AddWithValue("@NumberOfStates", MultistateValue.m_PROP_NUMBER_OF_STATES);
                    command.Parameters.AddWithValue("@NumberOfStatesName", NameState);
                    command.Parameters.AddWithValue("@PresentValue", MultistateValue.internal_PROP_PRESENT_VALUE);
                    command.ExecuteNonQuery();
                }
                connection.Close();
            }
        }
        public void PointUpdate(int BacID,MultiStateOutput MultistateValue)
        {
            using (SqliteConnection connection = new SqliteConnection(this.ConnectionString))
            {
                connection.Open();             
                using (SqliteCommand command = new SqliteCommand(
                   "UPDATE MultistateValue SET PresentValue = @PresentValue " +
                   "WHERE BacNetID = @BacNetID AND ObjID = @ObjID;", connection))
                {
                    command.Parameters.AddWithValue("@BacNetID", BacID);
                    command.Parameters.AddWithValue("@ObjID", MultistateValue.m_PROP_OBJECT_IDENTIFIER.instance);
                    command.Parameters.AddWithValue("@PresentValue", MultistateValue.internal_PROP_PRESENT_VALUE);
                    command.ExecuteNonQuery();
                }
                connection.Close();
            }
        }
        public void PointUpdate(int BacID, AnalogValue<float> AnalogValue)
        {
            using (SqliteConnection connection = new SqliteConnection(this.ConnectionString))
            {
                connection.Open();


                using (SqliteCommand command = new SqliteCommand(
                   "UPDATE AnalogValue SET PresentValue = @PresentValue " +
                   "WHERE BacNetID = @BacNetID AND ObjID = @ObjID;", connection))
                {
                    command.Parameters.AddWithValue("@BacNetID", BacID);
                    command.Parameters.AddWithValue("@ObjID", AnalogValue.m_PROP_OBJECT_IDENTIFIER.instance);
                    command.Parameters.AddWithValue("@PresentValue", AnalogValue.internal_PROP_PRESENT_VALUE);
                    command.ExecuteNonQuery();
                }

                connection.Close();
            }

        }
        public void PointUpdate(int BacID, BinaryOutput DigitalValue)
        {
            using (SqliteConnection connection = new SqliteConnection(this.ConnectionString))
            {
                connection.Open();


                using (SqliteCommand command = new SqliteCommand(
                   "UPDATE DigitalValue SET PresentValue = @PresentValue " +
                   "WHERE BacNetID = @BacNetID AND ObjID = @ObjID;", connection))
                {
                    command.Parameters.AddWithValue("@BacNetID", BacID);
                    command.Parameters.AddWithValue("@ObjID", DigitalValue.m_PROP_OBJECT_IDENTIFIER.instance);
                    command.Parameters.AddWithValue("@PresentValue", DigitalValue.internal_PROP_PRESENT_VALUE);
                    command.ExecuteNonQuery();
                }

                connection.Close();
            }

        }


    }
}
