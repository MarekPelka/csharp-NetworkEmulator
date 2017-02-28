﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ClientWindow;
using ManagementApp;
using Management;
using ControlCCRC.Protocols;

namespace NetNode
{
    class ControlAgent
    {
        private TcpListener listener;
        public int port;
        private string virtualIp;

        private static BinaryReader reader;
        private static BinaryWriter writer;

        public ControlAgent(int port, string ip)
        {
            this.port = port;
            this.virtualIp = ip;

            if(port != 0)
            {
                Thread thread = new Thread(new ThreadStart(Listen));
                thread.Start();
            }
        }

        private void Listen()
        {
            TcpClient clienttmp = new TcpClient("127.0.0.1", this.port);
            reader = new BinaryReader(clienttmp.GetStream());
            writer = new BinaryWriter(clienttmp.GetStream());
            try
            {
                while (true)
                {
                    string received_data = reader.ReadString();
                    JSON received_object = JSON.Deserialize(received_data);
                    CCtoCCSignallingMessage received_Protocol = received_object.Value.ToObject<CCtoCCSignallingMessage>();

                    if (received_Protocol.State == CCtoCCSignallingMessage.CC_UP_FIB_CHANGE)
                    {
                        //insert FIB
                        NetNode.log(DateTime.Now.ToLongTimeString() + " [CC -> node] insertFib", ConsoleColor.Yellow);
                        List<FIB> rec = received_Protocol.Fib_table;

                        //TODO allocate resources
                        foreach (var row in rec)
                        {
                            if (LRM.allocateResource(row.iport, row.in_cont))
                            {
                                NetNode.log(DateTime.Now.ToLongTimeString() + " [CC -> node] allocate: " + row.iport + " " + row.in_cont, ConsoleColor.Yellow);
                                if (LRM.allocateResource(row.oport, row.out_cont))
                                {
                                    NetNode.log(DateTime.Now.ToLongTimeString() + " [CC -> node] allocate: " + row.oport + " " + row.out_cont, ConsoleColor.Yellow);
                                    sendTopologyAllocated(row.iport, row.in_cont);
                                    sendTopologyAllocated(row.oport, row.out_cont);
                                    SwitchingField.addToSwitch(row);
                                    SwitchingField.addToSwitch(new FIB(row.oport, row.out_cont, row.iport, row.in_cont));
                                    sendConfirmation(row.iport, row.in_cont, true);
                                }
                                else
                                {
                                    LRM.deallocateResource(row.iport, row.in_cont);
                                    sendConfirmation(row.iport, row.in_cont, false);
                                }
                            }
                            else
                            {
                                sendConfirmation(row.iport, row.in_cont, false);
                            }
                        }
                    }
                    else if (received_Protocol.State == CCtoCCSignallingMessage.REALEASE_TOP_BOTTOM)
                    {
                        foreach (var fib in received_Protocol.Fib_table)
                        {
                            NetNode.log(DateTime.Now.ToLongTimeString() + " [CC -> LRM]" + " deallocate" + fib.toString(), ConsoleColor.Green);
                          if(LRM.deallocateResource(fib.iport,fib.in_cont))
                          {
                              sendTopologyDeallocated(fib.iport, fib.in_cont);
                          }
                          if(LRM.deallocateResource(fib.oport, fib.out_cont))
                          {
                              sendTopologyDeallocated(fib.oport, fib.out_cont);
                          }
                          SwitchingField.clearFib(fib);
                          SwitchingField.clearFib(new FIB(fib.oport,fib.out_cont,fib.iport,fib.in_cont));
                        }
                    }
                    else
                    {
                        NetNode.log("undefined protocol", ConsoleColor.Red);
                    }
                }
            }
            catch (Exception e)
            {
                NetNode.log("\nError sending signal: " + e.Message, ConsoleColor.Red);
                Thread.Sleep(2000);
                Environment.Exit(1);
            }
        }

        public static void sendTopologyInit(string ip)
        {
            NetNode.log(DateTime.Now.ToLongTimeString() + " [LRM -> RC] inittopology: " + ip, ConsoleColor.Yellow);

            RCtoLRMSignallingMessage protocol = new RCtoLRMSignallingMessage();
            protocol.State = RCtoLRMSignallingMessage.LRM_INIT;
            protocol.NodeName = ip;
            String send_object = JMessage.Serialize(JMessage.FromValue(protocol));
            writer.Write(send_object);
        }

        public static void sendTopology(string from, int port, string to)
        {
            string toSend = port.ToString() + " " + to;
            NetNode.log(DateTime.Now.ToLongTimeString() + " [LRM -> RC] LocalTopology : " + toSend, ConsoleColor.Yellow);

            RCtoLRMSignallingMessage protocol = new RCtoLRMSignallingMessage();
            protocol.State = RCtoLRMSignallingMessage.LRM_TOPOLOGY_ADD;
            protocol.ConnectedNodePort = port;
            protocol.ConnectedNode = to;
            String send_object = JMessage.Serialize(JMessage.FromValue(protocol));
            writer.Write(send_object);
        }

        public static void sendTopologyAllocated(int port, int no_vc3)
        {
            string to;
            LRM.connections.TryGetValue(port, out to);
            NetNode.log(DateTime.Now.ToLongTimeString() + " [LRM -> RC] topology allocated: " + to + " " + no_vc3, ConsoleColor.Yellow);

            RCtoLRMSignallingMessage protocol = new RCtoLRMSignallingMessage();
            protocol.State = RCtoLRMSignallingMessage.LRM_TOPOLOGY_ALLOCATED;
            protocol.ConnectedNode = to;
            protocol.AllocatedSlot = no_vc3;
            String send_object = JMessage.Serialize(JMessage.FromValue(protocol));
            writer.Write(send_object);
        }

        public static void sendTopologyDeallocated(int port, int no_vc3)
        {
            string to;
            LRM.connections.TryGetValue(port, out to);
            NetNode.log(DateTime.Now.ToLongTimeString() + " [LRM -> RC] topology deallocated: " + to + " " + no_vc3, ConsoleColor.Red);

            RCtoLRMSignallingMessage protocol = new RCtoLRMSignallingMessage();
            protocol.State = RCtoLRMSignallingMessage.LRM_TOPOLOGY_DEALLOCATED;
            protocol.ConnectedNode = to;
            protocol.AllocatedSlot = no_vc3;
            String send_object = JMessage.Serialize(JMessage.FromValue(protocol));
            writer.Write(send_object);
        }

        public static void sendDeleted(string from, int port, string to)
        {
            string toSend = port.ToString() + " " + to;
            NetNode.log(DateTime.Now.ToLongTimeString() + " [LRM -> RC] info about deletion: " + toSend, ConsoleColor.Yellow);

            RCtoLRMSignallingMessage protocol = new RCtoLRMSignallingMessage();
            protocol.State = RCtoLRMSignallingMessage.LRM_TOPOLOGY_DELETE;
            //protocol.ConnectedNodePort = port;
            protocol.ConnectedNode = to;
            String send_object = JMessage.Serialize(JMessage.FromValue(protocol));
            writer.Write(send_object);
        }

        public static void sendDeletedCC(string from, int port, string to)
        {
            string toSend = port.ToString() + " " + to;
            NetNode.log(DateTime.Now.ToLongTimeString() + " [node -> CC] info about deletion: " + toSend, ConsoleColor.Yellow);

            CCtoCCSignallingMessage protocol = new CCtoCCSignallingMessage();
            protocol.State = CCtoCCSignallingMessage.RE_ROUTE_QUERY;
            String send_object = JMessage.Serialize(JMessage.FromValue(protocol));
            writer.Write(send_object);
        }

        public static void sendConfirmation(int port, int no_vc3, bool flag)
        {
            CCtoCCSignallingMessage protocol = new CCtoCCSignallingMessage();

            if (flag == true)
            {
                protocol.State = CCtoCCSignallingMessage.CC_LOW_CONFIRM;
                NetNode.log(DateTime.Now.ToLongTimeString() + " [node -> CC] CONFIRM", ConsoleColor.Green);
            }
            else
            {
                protocol.State = CCtoCCSignallingMessage.CC_LOW_REJECT;
                NetNode.log(DateTime.Now.ToLongTimeString() + " [node -> CC] REJECT", ConsoleColor.Red);
            }
            String send_object = JMessage.Serialize(JMessage.FromValue(protocol));
            writer.Write(send_object);
        }
    }
}
