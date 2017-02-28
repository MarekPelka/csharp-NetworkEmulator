﻿using ClientWindow;
using ControlCCRC.Protocols;
using Management;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ManagementApp;

namespace ControlCCRC
{
    class ConnectionController
    {
        private static readonly object ConsoleWriterLock = new object();
        private String identifier;

        private TcpClient CCClient;
        private TcpClient NCCClient;

        private Thread threadconnectCC;
        private BinaryWriter ccWriter;
        private Thread threadconnectNCC;
        private BinaryWriter nccWriter;

        private RoutingController rcHandler;
        private Dictionary<String, BinaryWriter> socketHandler;

        private Boolean iAmDomain;
        /**
         * DOMAIN [CC_ID, connect NCC]
         * SUBNETWORK [CC_ID, connect up CC, flag] 
         */
        public ConnectionController(string[] args)
        {
            iAmDomain = (args.Length == 2);
            identifier = args[0];

            if (iAmDomain)
            {
                consoleWriter("[INIT] DOMAIN - " + identifier);
                try
                {
                    int nccPort;
                    int.TryParse(args[1], out nccPort);
                    Thread.Sleep(500);
                    NCCClient = new TcpClient("localhost", nccPort);
                }
                catch (SocketException ex)
                {
                    consoleWriter("[ERROR] Cannot connect with NCC.");
                }
                this.threadconnectNCC = new Thread(new ThreadStart(nccConnect));
                threadconnectNCC.Start();
            }
            else
            {
                consoleWriter("[INIT] SUBNETWORK - " + identifier);
                try
                {
                    int ccPort;
                    int.TryParse(args[1], out ccPort);
                    CCClient = new TcpClient("localhost", ccPort);
                }
                catch (SocketException ex)
                {
                    consoleWriter("[ERROR] Cannot connect with upper CC.");
                }
                this.threadconnectCC = new Thread(new ThreadStart(ccConnect));
                threadconnectCC.Start();
            }


            consoleStart();
        }

        public void setRCHandler(RoutingController rc)
        {
            this.rcHandler = rc;
        }

        public void setSocketHandler(Dictionary<String, BinaryWriter> socketHandler)
        {
            this.socketHandler = socketHandler;
        }

        private void nccConnect()
        {
            BinaryReader reader = new BinaryReader(NCCClient.GetStream());
            nccWriter = new BinaryWriter(NCCClient.GetStream());


            CCtoNCCSingallingMessage initMsg = new CCtoNCCSingallingMessage();
            initMsg.State = CCtoNCCSingallingMessage.INIT_FROM_CC;
            String dataToSend = JSON.Serialize(JSON.FromValue(initMsg));
            nccWriter.Write(dataToSend);
            socketHandler.Add("NCC", nccWriter);

            Boolean noError = true;
            while (noError)
            {
                try
                {
                    string received_data = reader.ReadString();
                    JSON received_object = JSON.Deserialize(received_data);
                    if (received_object.Type != typeof(CCtoNCCSingallingMessage))
                        noError = false;
                    CCtoNCCSingallingMessage msg = received_object.Value.ToObject<CCtoNCCSingallingMessage>();
                    switch (msg.State)
                    {
                        case CCtoNCCSingallingMessage.NCC_SET_CONNECTION:      
                            consoleWriter("[NCC -> CC(mine)] ConnectionRequest( " + msg.NodeFrom 
                                + " , " + msg.NodeTo + " ) (" + msg.Rate + "x VC-3 , requestId=" + msg.RequestID + " ) ");
                            consoleWriter("[CC(mine) -> RC(mine)] RouteQuery( " + msg.NodeFrom
                                + " , " + msg.NodeTo + " ) (" + msg.Rate + "x VC-3 , requestId=" + msg.RequestID + " ) ");
                            rcHandler.initConnectionRequestFromCC(msg.NodeFrom, msg.NodeTo, msg.Rate, msg.RequestID, msg.Vc11, msg.Vc12, msg.Vc13, false);
                            break;
                        case CCtoNCCSingallingMessage.NCC_RELEASE_WITH_ID:
                            consoleWriter("[NCC -> CC(mine)] CallRelease( " + msg.RequestID + " ) ");
                            rcHandler.releaseConnection(msg.RequestID);
                            break;
                    }
                }
                catch (IOException ex)
                {
                    Environment.Exit(1);
                }
            }
        }

        private void ccConnect()
        {
            BinaryReader reader = new BinaryReader(CCClient.GetStream());
            ccWriter = new BinaryWriter(CCClient.GetStream());


            CCtoCCSignallingMessage initMsg = new CCtoCCSignallingMessage();
            initMsg.Identifier = identifier;
            initMsg.State = CCtoCCSignallingMessage.CC_MIDDLE_INIT;
            String send_object = JSON.Serialize(JSON.FromValue(initMsg));
            ccWriter.Write(send_object);


            Boolean noError = true;
            while (noError)
            {
                try
                {
                    string received_data = reader.ReadString();
                    JSON received_object = JSON.Deserialize(received_data);
                    if (received_object.Type != typeof(CCtoCCSignallingMessage))
                        noError = false;
                    CCtoCCSignallingMessage msg = received_object.Value.ToObject<CCtoCCSignallingMessage>();
                    
                    switch(msg.State)
                    {
                        case CCtoCCSignallingMessage.CC_BUILD_PATH_REQUEST:
                            
                            rcHandler.initConnectionRequestFromCC(msg.NodeFrom,
                                msg.NodeTo, msg.Rate, msg.RequestId, msg.Vc1, msg.Vc2, msg.Vc3, false);
                            consoleWriter("[CC(upper) -> CC(mine)] Prepare weights( " + msg.NodeFrom
                                + " , " + msg.NodeTo + " ) (" + msg.Rate + "x VC-3 , requestId=" + msg.RequestId + " )");
                            consoleWriter("[CC(mine) -> RC(mine)] RouteQuery( " + msg.NodeFrom
                               + " , " + msg.NodeTo + " ) (" + msg.Rate + "x VC-3 , requestId=" + msg.RequestId + " ) ");
                            break;
                        case CCtoCCSignallingMessage.FIB_SETTING_TOP_BOTTOM:
                            
                            rcHandler.startProperWeigthComputingTopBottom(new Dictionary<string, Dictionary<string, int>>(),
                                      new Dictionary<string, string>(), msg.Rate, "",
                                      msg.NodeFrom, msg.NodeTo);
                            consoleWriter("[CC(upper) -> CC(mine)] ConnectionRequest( " + msg.NodeFrom
                                + " , " + msg.NodeTo + " ) (" + msg.Rate + "x VC-3 , requestId=" + msg.RequestId + " ) \n");
                            consoleWriter("[CC(mine) -> RC(mine)] RouteQuery( " + msg.NodeFrom
                              + " , " + msg.NodeTo + " ) (" + msg.Rate + "x VC-3 , requestId=" + msg.RequestId + " ) ");
                            break;
                        case CCtoCCSignallingMessage.REALEASE_TOP_BOTTOM:
                            rcHandler.releaseConnection(msg.RequestId);
                            consoleWriter("[CC(upper) -> CC(mine)] CallRelease( " + msg.RequestId + " ) ");
                            break;
                    }
                }
                catch (IOException ex)
                {
                    Environment.Exit(1);
                }
            }
        }

        internal void reRouteInit()
        {
            consoleWriter("[CC(lower) -> CC(mine)] soft re-routing( "+rcHandler.requestId+" ) ");
            consoleWriter("[CC(mine) -> RC(mine)] RouteQuery( ) ");
            rcHandler.reRouteQuery();
        }

        private void consoleStart()
        {
        }

        private void consoleWriter(String msg)
        {
            lock (ConsoleWriterLock)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.BackgroundColor = ConsoleColor.White;
                Console.Write("#[CC]" + DateTime.Now.ToLongTimeString() + " #:" + msg);
                Console.Write(Environment.NewLine);
            }
        }

        internal void sendRequestToSubnetworkCCToBuildPath(string rcName, string nodeFrom, string nodeTo, int rate, int requestId)
        {
            consoleWriter("[CC(upper) -> CC(mine)] Prepare weights( " + nodeFrom
                               + " , " + nodeTo + " ) (" + rate + "x VC-3 , requestId=" + requestId + " ) ");
            CCtoCCSignallingMessage ccRequest = new CCtoCCSignallingMessage();
            ccRequest.State = CCtoCCSignallingMessage.CC_BUILD_PATH_REQUEST;
            ccRequest.NodeFrom = nodeFrom;
            ccRequest.NodeTo = nodeTo;
            ccRequest.Rate = rate;
            ccRequest.RequestId = requestId;
            if (rcHandler.vc1Forbidden)
                ccRequest.Vc1 = 0;
            else
                ccRequest.Vc1 = 1;

            if (rcHandler.vc2Forbidden)
                ccRequest.Vc2 = 0;
            else
                ccRequest.Vc2 = 1;

            if (rcHandler.vc3Forbidden)
                ccRequest.Vc3 = 0;
            else
                ccRequest.Vc3 = 1;

            String dataToSend = JSON.Serialize(JSON.FromValue(ccRequest));
            socketHandler["CC_" + rcName.Substring(rcName.IndexOf("_") + 1)].Write(dataToSend);
        }

        internal void sendFibs(Dictionary<string, List<FIB>> dictionary, int using1, int using2, int using3, int requestId, int rate, bool routed)
        {
            if (dictionary != null)
            {
                foreach (string nodeName in dictionary.Keys)
                {
                    consoleWriter("[CC(mine) -> CC(in node)] SetFIBS( requestId=" + requestId + " ) ");
                    CCtoCCSignallingMessage fibsMsg = new CCtoCCSignallingMessage();
                    fibsMsg.State = CCtoCCSignallingMessage.CC_UP_FIB_CHANGE;
                    fibsMsg.Fib_table = dictionary[nodeName];
                    String dataOut = JSON.Serialize(JSON.FromValue(fibsMsg));
                    socketHandler[nodeName].Write(dataOut);
                }
            }
            if (routed)
                return;
            if(iAmDomain)
            {
                if (dictionary != null)
                {
                    consoleWriter("[CC -> NCC] ConnectionConfirmed( " + requestId + " ) ");
                    CCtoNCCSingallingMessage finishMsg = new CCtoNCCSingallingMessage();
                    finishMsg.State = CCtoNCCSingallingMessage.CC_CONFIRM;
                    finishMsg.Rate = rate;
                    finishMsg.Vc11 = using1;
                    finishMsg.Vc12 = using2;
                    finishMsg.Vc13 = using3;
                    finishMsg.NodeTo = dictionary.Keys.First();
                    foreach(String connectedNode in 
                        rcHandler.wholeTopologyNodesAndConnectedNodesWithPorts[dictionary.Keys.Last()].Keys)
                    {
                        if (connectedNode.StartsWith("192"))
                        {
                            finishMsg.NodeFrom = connectedNode;
                        }
                            
                    }
                    finishMsg.RequestID = requestId;
                    String dataToSend = JSON.Serialize(JSON.FromValue(finishMsg));
                    nccWriter.Write(dataToSend);
                }
                else
                {
                    consoleWriter("[CC -> NCC] ConnectionRejected( " + requestId + " ) ");
                    CCtoNCCSingallingMessage finishMsg = new CCtoNCCSingallingMessage();
                    finishMsg.State = CCtoNCCSingallingMessage.CC_REJECT;
                    finishMsg.Rate = rate;
                    finishMsg.Vc11 = using1;
                    finishMsg.Vc12 = using2;
                    finishMsg.Vc13 = using3;
                    finishMsg.RequestID = requestId;
                    String dataToSend = JSON.Serialize(JSON.FromValue(finishMsg));
                    nccWriter.Write(dataToSend);
                }
            }
        }

        public void sendRealeseFibs(Dictionary<string, List<FIB>> dictionary,int requestId)
        {
            foreach (string nodeName in dictionary.Keys)
            {
                consoleWriter("[CC(mine) -> CC(in node)] CallRelease( " + requestId + " ) ");
                CCtoCCSignallingMessage fibsMsg = new CCtoCCSignallingMessage();
                fibsMsg.State = CCtoCCSignallingMessage.REALEASE_TOP_BOTTOM;
                fibsMsg.Fib_table = dictionary[nodeName];
                String dataOut = JSON.Serialize(JSON.FromValue(fibsMsg));
                socketHandler[nodeName].Write(dataOut);
            }
        }


        public void sendBorderNodesToNCC(Address adr, int domain)
        {
            CCtoNCCSingallingMessage borderNodeMsg = new CCtoNCCSingallingMessage();
            borderNodeMsg.State = CCtoNCCSingallingMessage.BORDER_NODE;
            borderNodeMsg.BorderNode = adr.getName();
            borderNodeMsg.BorderDomain = domain;
            String dataToSend = JSON.Serialize(JSON.FromValue(borderNodeMsg));
            nccWriter.Write(dataToSend);
        }

        public void sendFIBSettingRequestForSubnetwork(String nodeFrom, String nodeTo, String rcName,int rate)
        {
            consoleWriter("[CC(mine) -> CC(lower)] ConnectionRequest( " + nodeFrom
                                 + " , " + nodeTo + " ) (" + rate + "x VC-3 , requestId=" + rcHandler.requestId + " ) ");
            String ccName = rcName.Replace("RC", "CC"); ;
            CCtoCCSignallingMessage setFIBmsg = new CCtoCCSignallingMessage();
            setFIBmsg.State = CCtoCCSignallingMessage.FIB_SETTING_TOP_BOTTOM;
            setFIBmsg.NodeFrom = nodeFrom;
            setFIBmsg.NodeTo = nodeTo;
            setFIBmsg.Rate = rate;
            setFIBmsg.RequestId = rcHandler.requestId;
            String dataToSend = JSON.Serialize(JSON.FromValue(setFIBmsg));
            socketHandler[ccName].Write(dataToSend);
        }

        internal void sendFIBRealeaseForSubnetwork(String rcName, int requestIdToRealese)
        {
            consoleWriter("[CC(mine) -> CC(lower)] CallRelease( " + requestIdToRealese + " ) ");
            String ccName = rcName.Replace("RC", "CC"); ;
            CCtoCCSignallingMessage setFIBmsg = new CCtoCCSignallingMessage();
            setFIBmsg.State = CCtoCCSignallingMessage.REALEASE_TOP_BOTTOM;
            setFIBmsg.RequestId = requestIdToRealese;
            String dataToSend = JSON.Serialize(JSON.FromValue(setFIBmsg));
            socketHandler[ccName].Write(dataToSend);
        }
    }
}
