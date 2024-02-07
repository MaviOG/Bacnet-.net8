using System;
using System.Net.Sockets;
using NModbus;
using NModbus.Device;
namespace ModbusTCPClientV1
{ 
    public class ModbusTCPClient
    {
        public string SERVER_IP = "";
        public int SERVER_PORT = 502;
        public byte UNIT_ID = 1;
        public ModbusTCPClient(string Server_IP,int Server_port,byte unit_id)
        {
            this.SERVER_IP = Server_IP;
            this.SERVER_PORT = Server_port;
            this.UNIT_ID = unit_id;
        }
        public bool[] ReadCoils( ushort startAddress, ushort count)
        {
            try
            {
                using (TcpClient client = new TcpClient(this.SERVER_IP, this.SERVER_PORT))
                {
                    var factory = new ModbusFactory();
                    IModbusMaster master = factory.CreateMaster(client);


                    bool[] data = master.ReadCoils(this.UNIT_ID, startAddress, count);
                    return data;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return null;
            }
        }
        public bool[] ReadDiscreteInputs(ushort startAddress, ushort count)
        {
            try
            {
                using (TcpClient client = new TcpClient(this.SERVER_IP, this.SERVER_PORT))
                {
                    var factory = new ModbusFactory();
                    IModbusMaster master = factory.CreateMaster(client);
                    bool[] data = master.ReadInputs(this.UNIT_ID, startAddress, count);
                    return data;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return null;
            }
        }
        public ushort[] ReadHoldingRegisters(ushort startAddress, ushort count)
        {
            try
            {
                using (TcpClient client = new TcpClient(this.SERVER_IP, this.SERVER_PORT))
                {
                    var factory = new ModbusFactory();
                    IModbusMaster master = factory.CreateMaster(client);
                    ushort[] data = master.ReadHoldingRegisters(this.UNIT_ID, startAddress, count);
                    return data;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return null;
            }
        }
        public ushort[] ReadInputRegisters(ushort startAddress, ushort count)
        {
            try
            {
                using (TcpClient client = new TcpClient(this.SERVER_IP, this.SERVER_PORT))
                {
                    var factory = new ModbusFactory();
                    IModbusMaster master = factory.CreateMaster(client);
                    ushort[] data = master.ReadInputRegisters(this.UNIT_ID, startAddress, count);
                    return data;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return null;
            }
        }
        public void WriteSingleCoil(ushort address, bool value)
        {
            try
            {
                using (TcpClient client = new TcpClient(this.SERVER_IP, this.SERVER_PORT))
                {
                    var factory = new ModbusFactory();
                    IModbusMaster master = factory.CreateMaster(client);
                    master.WriteSingleCoil(this.UNIT_ID, address, value);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
        public void WriteSingleRegister(ushort address, ushort value)
        {
            try
            {
                using (TcpClient client = new TcpClient(this.SERVER_IP, this.SERVER_PORT))
                {
                    var factory = new ModbusFactory();
                    IModbusMaster master = factory.CreateMaster(client);
                    master.WriteSingleRegister(this.UNIT_ID, address, value);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}