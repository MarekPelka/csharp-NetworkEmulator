﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using ManagementApp;


namespace ControlNCC
{
    class NetworkCallControl
    {
        private int controlPort;
        private TcpListener listener;
        private Dictionary<int, ControlConnectionService> services;
        private Dictionary<int, int> cpccRequests;
        public int domainNumber;
        private ControlConnectionService CCService;
        public ManagementHandler management;
        private int managementPort;
        public Dictionary<int, int> interdomainRequests;
        //private Dictionary<string, int> interdomainCalls;
        private Dictionary<int, List<string>> intrerdomainCallsAttempts;
        private Dictionary<int, string> CNAddressesForInterdomainCalls;
        private Dictionary<string, int> borderGateways;
        public Dictionary<int, string> rejectedDestinations;
        Random r;
        public NetworkCallControl(string[] domainParams)
        {
            r = new Random();
            services = new Dictionary<int, ControlConnectionService>();
            cpccRequests = new Dictionary<int, int>();
            interdomainRequests = new Dictionary<int, int>();
            CNAddressesForInterdomainCalls = new Dictionary<int, string>();
            borderGateways = new Dictionary<string, int>();
            rejectedDestinations = new Dictionary<int, string>();
            //interdomainCalls = new Dictionary<int, string>();
            intrerdomainCallsAttempts = new Dictionary<int, List<string>>();
            string ip = "127.0.0.1";
            int.TryParse(domainParams[0], out domainNumber);
            Console.WriteLine("Domain: " + domainNumber + " Listener: " + domainParams[1] + " Management: " + domainParams[2]);
            //readConfig();
            int.TryParse(domainParams[1], out this.controlPort);
            listener = new TcpListener(IPAddress.Parse(ip), controlPort);
            Thread thread = new Thread(new ThreadStart(Listen));
            thread.Start();
            Console.WriteLine("[INIT] Start NCC, IP: " + ip + " Port: " + controlPort);

            int.TryParse(domainParams[2], out this.managementPort);
            management = new ManagementHandler(this.managementPort, this);
        }

        private void Listen()
        {
            listener.Start();

            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                ControlConnectionService service = new ControlConnectionService(client, this);
            }
        }

        public void addService(int ID, ControlConnectionService handler)
        {
            services.Add(ID, handler);
        }

        public ControlConnectionService getService(int ID)
        {
            return services[ID];
        }

        public void setCCService(ControlConnectionService handler)
        {
            this.CCService = handler;
        }

        public ControlConnectionService getCCService()
        {
            return this.CCService;
        }

        public void addCpccRequest(int requestID, int cpccService)
        {
            this.cpccRequests.Add(requestID, cpccService);
        }

        public void removeCpccRequest(int requestID)
        {
            this.cpccRequests.Remove(requestID);
        }

        public int getCpccService(int requestID)
        {
            return this.cpccRequests[requestID];
        }

        public ControlConnectionService getCpccServiceByAddr(string destinationAddress)
        {
            Address address = new Address(destinationAddress);
            int CPCCID = address.type + address.domain + address.subnet + address.space;
            return this.services[CPCCID];
        }


        public void addInterdomainRequest(int requestID, int domainID)
        {
            interdomainRequests.Add(requestID, domainID);
        }

        public bool checkIfInterdomainRequest(int requestID)
        {
            int domainID;
            interdomainRequests.TryGetValue(requestID, out domainID);
            if (domainID != 0)
            {
                return true;
            }
            else
                return false;
        }

        public int getDomainService(int requestID)
        {
            return interdomainRequests[requestID];
        }

        public void addCNAddressesForInterdomainCalls(int requestID, string clientIdentifier)
        {
            CNAddressesForInterdomainCalls.Add(requestID, clientIdentifier);
        }

        public string getCNAddressesForInterdomainCalls(int requestID)
        {
            return CNAddressesForInterdomainCalls[requestID];
        }
        public void clearCNAddressesForInterdomainCalls(int requestID)
        {
            CNAddressesForInterdomainCalls.Remove(requestID);
        }

        public void addBorderGateway(int domain, string address)
        {
            borderGateways.Add(address, domain);
        }
        public List<string> returnBorderGateway(int domain)
        {
            List<string> res = new List<string>();
            
            foreach(var addresDomainPair in borderGateways)
            {
                if(addresDomainPair.Value == domain)
                {
                    res.Add(addresDomainPair.Key);
                }
            }
            List<int> sortedResult = res.Select(n => new Address(n).space).ToList();
            sortedResult.Sort();

            res = res.OrderBy(n => n).ToList();

            return res;
        }

        //public void addInterdomainCall(string borderGWaddress, int interdomainRequestID)
        //{
        //    interdomainCalls.Add(borderGWaddress, interdomainRequestID);
        //}
        public bool checkIfInterdomainCall(int interdomainRequestID)
        {
            bool result = false;
            foreach (var borderGwRequestIDPair in intrerdomainCallsAttempts)
            {
                if (borderGwRequestIDPair.Key == interdomainRequestID)
                {
                    result = true;
                    break;
                }
            }
            return result;
        }
        //addresTo get z listy interdomain calls 
        public string getAnotherBorderGatewayAddress(int interdomainRequestID, string addressToGetDomain)
        {
            Address tmpAddress = new Address(addressToGetDomain);
            List<string> borderGWAddresses = new List<string>();
            borderGWAddresses = returnBorderGateway(tmpAddress.domain);
            string result = null;
            foreach(String used in intrerdomainCallsAttempts[interdomainRequestID])
            {
                //Console.WriteLine("UZYTY id " + interdomainRequestID + ": " + used);
            }
           for (int i = 0; i < borderGWAddresses.Count; i++)
            {
                if(!intrerdomainCallsAttempts[interdomainRequestID].Contains(borderGWAddresses[i]))
                    return borderGWAddresses[i];
            }

            return result;
        }
        public void initInterdomanCallTask(int interdomainRequestID, string borderGWaddress)
        {
            intrerdomainCallsAttempts.Add(interdomainRequestID, new List<string>());
            intrerdomainCallsAttempts[interdomainRequestID].Add(borderGWaddress);
        }
        public void addIntrerdomainCallsAttempts(int interdomainRequestID, string borderGWaddress)
        {
            intrerdomainCallsAttempts[interdomainRequestID].Add(borderGWaddress);
        }

        public void showInterdomainAttemptsForRequestID(int requestID)
        {
            //foreach (string addres in intrerdomainCallsAttempts[requestID])
               // Console.WriteLine("Próby dla "+requestID+" :"+addres);

        }

        public void clearInterdomainCallAttempt(int interdomainCallRequestIDToClear)
        {

            intrerdomainCallsAttempts.Remove(interdomainCallRequestIDToClear);
        }



      

        public Boolean checkIfInDirectory(string address)
        {
            Address addres = new Address(address);
            if (addres.domain == domainNumber)
            {
                return true;
            }
            else
            {
                return false;
            }


        }

        public int generateRequestID()
        {
            int reqID = r.Next(10000, 40000);
            return reqID;
        }

        public void consoleWriter(string msg)
        {
            log(DateTime.Now.ToLongTimeString() + ": " + msg, ConsoleColor.Cyan);
        }

        public static void log(string msg, ConsoleColor cc)
        {
            Console.ForegroundColor = cc;
            Console.WriteLine(msg);
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}
