﻿using ClientWindow;
using ManagementApp;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CableCloud
{
    class CloudLogic
    {
        private const string ERROR_MSG = "ERROR: ";
        private const ConsoleColor ERROR_COLOR = ConsoleColor.Red;
        private const ConsoleColor ADMIN_COLOR = ConsoleColor.Green;
        private const ConsoleColor INFO_COLOR = ConsoleColor.Blue;

        /** TABLE WITH CONNECTION */
        private DataTable tableWithPorts;

        /** HANDLERS MAP - localPORT-Thread with connection to this port */
        private Dictionary<String, NodeConnectionThread> portToThreadMap;

        public CloudLogic()
        {
            tableWithPorts = new DataTable("Connections");
            portToThreadMap = new Dictionary<String, NodeConnectionThread>();
            tableWithPorts.Columns.Add("fromPort", typeof(int)).AllowDBNull = false;
            tableWithPorts.Columns.Add("virtualFromPort", typeof(int)).AllowDBNull = false;
            tableWithPorts.Columns.Add("toPort", typeof(int)).AllowDBNull = false;
            tableWithPorts.Columns.Add("virtualToPort", typeof(int)).AllowDBNull = false;
 
            /** LOGS CONSOLE  */
            consoleWriter("Cloud start");
        }

        public void connectToWindowApplication(int port)
        {
            TcpClient connection = null;
            try
            {
                connection = new TcpClient("localhost", port);
            }
            catch(SocketException ex)
            {
                consoleWriter("ERROR: Cannot connect with window application.");
            }
            Thread clientThread = new Thread(new ParameterizedThreadStart(windowConnectionThread));
            clientThread.Start(connection);
        }

        private void windowConnectionThread(Object connection)
        {
            consoleWriter("Connected with window application");
            TcpClient clienttmp = (TcpClient)connection;
            BinaryReader reader = new BinaryReader(clienttmp.GetStream());
            while (true)
            {
                string received_data = null;
                try
                {
                    received_data = reader.ReadString();
                    JSON receivedMessage = null;
                    receivedMessage = JSON.Deserialize(received_data);
                    if (receivedMessage.Type == typeof(ConnectionProperties))
                    {
                        ConnectionProperties received_connection = receivedMessage.Value.ToObject<ConnectionProperties>();
                        if (received_connection.LocalPortTo == 0 && received_connection.VirtualPortTo == 0)
                        {
                            deleteCable(received_connection.LocalPortFrom, received_connection.VirtualPortFrom);
                            consoleWriter("Deleted connection: real port:" + received_connection.LocalPortFrom +
                                "virtual port:" + received_connection.VirtualPortFrom);
                            continue;
                        }
                        try
                        {
                            connectToNodes(received_connection.LocalPortFrom, received_connection.VirtualPortFrom,
                               received_connection.LocalPortTo, received_connection.VirtualPortTo);
                        }
                        catch (SocketException ex)
                        {
                            consoleWriter("Connection can't be made on port " + received_connection.LocalPortFrom);
                            return;
                        }
                    }
                    else if (receivedMessage.Type == typeof(List<ConnectionProperties>))
                    {
                        List<ConnectionProperties> received_connections = receivedMessage.Value.ToObject<List<ConnectionProperties>>();
                        foreach (ConnectionProperties received_connection in received_connections)
                        {
                            
                            if (received_connection.LocalPortTo == 0 && received_connection.VirtualPortTo == 0)
                            {
                                deleteCable(received_connection.LocalPortFrom, received_connection.VirtualPortFrom);
                                consoleWriter("Deleted connection: real port:" + received_connection.LocalPortFrom +
                                    "virtual port:" + received_connection.VirtualPortFrom);
                                continue;
                            }
                            try
                            {
                                connectToNodes(received_connection.LocalPortFrom, received_connection.VirtualPortFrom,
                                   received_connection.LocalPortTo, received_connection.VirtualPortTo);
                            }
                            catch (SocketException ex)
                            {
                                consoleWriter("Connection can't be made on port " + received_connection.LocalPortFrom);
                                return;
                            }
                        }
                    }
                    else
                    {
                        consoleWriter(ERROR_MSG + "received from window application wrong data format.");
                    }
                }
                catch (IOException ex)
                {
                    break;
                }          
            }
        }

        public void connectToNodes(int fromPort, int virtualFromPort,
                                    int toPort, int virtualToPort) 
        {
            if (!portToThreadMap.ContainsKey(fromPort + ":" + virtualFromPort))
            {
                String connection1Name = +fromPort +
                 "(virtual:" + virtualFromPort + ")-->" + toPort +
                 "(virtual:" + virtualToPort + ")";
                NodeConnectionThread fromThread = new NodeConnectionThread(
                    ref portToThreadMap, tableWithPorts, connection1Name, fromPort, virtualFromPort,
                    toPort, virtualToPort);
                Thread.Sleep(150);
            }
            if (!portToThreadMap.ContainsKey(toPort + ":" + virtualToPort))
            {
                String connection2Name = toPort +
                 "(virtual:" + virtualToPort + ")-->" + fromPort +
                 "(virtual:" + virtualFromPort + ")";
                NodeConnectionThread toThread = new NodeConnectionThread(
                    ref portToThreadMap, tableWithPorts, connection2Name, toPort, virtualToPort, fromPort, virtualFromPort);
                Thread.Sleep(150);
            }
        }


        private void deleteCable(int fromPort, int virtualFromPort)
        {
            int toPort = 0;
            int virtualToPort = 0;
            for (int i = tableWithPorts.Rows.Count - 1; i >= 0; i--)
            {
                DataRow dr = tableWithPorts.Rows[i];
                if (dr["fromPort"].Equals(fromPort) && dr["virtualFromPort"].Equals(virtualFromPort))
                {
                    toPort = (int)dr["toPort"];
                    virtualToPort = (int)dr["virtualToPort"];
                    tableWithPorts.Rows.Remove(dr);
                    portToThreadMap.Remove(fromPort + ":" + virtualFromPort);
                }
            }
        }
        private void consoleWriter(String msg)
        {
            Console.ForegroundColor = ConsoleColor.Black;
            Console.BackgroundColor = ConsoleColor.White;

            Console.Write("#" + DateTime.Now.ToLongTimeString() + " " + DateTime.Now.ToLongDateString() + "#:" + msg);
            Console.Write(Environment.NewLine);
        }
    }
}
