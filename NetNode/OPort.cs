﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using ClientWindow;

namespace NetNode
{
    //output port
    class OPort
    {
        public int port;
        public Queue<STM1> output = new Queue<STM1>();
        public STM1 currentFrame = new STM1();

        public OPort(int port)
        {
            this.port = port;
        }

        public void addToOutQueue(VirtualContainer4 container)
        {
            STM1 temp = new STM1(container);
            this.output.Enqueue(temp);
            Console.WriteLine("Stm1 added to output queue");
        }
        public void addToTempQueue(VirtualContainer3 container, int pos)
        {
            if(this.currentFrame.vc4 == null)
            {
                this.currentFrame.vc4 = new VirtualContainer4();
            }
            if (pos != 0)
            {
                this.currentFrame.vc4.vc3List.Add(pos, container);
            }
        }
        public void addToOutQueue()
        {
            if (this.currentFrame.vc4 != null)
            {
                if (this.currentFrame.vc4.vc3List.Count > 0)
                {
                    Random r = new Random();
                    int POH = r.Next(30000, 50000);
                    Console.WriteLine("packing vc3's to vc4");
                    Console.WriteLine("added new POH");
                    this.currentFrame.vc4.POH = POH;
                    this.output.Enqueue(this.currentFrame);
                    this.clear();
                    Console.WriteLine("Stm1 added to output queue");
                }
            }
        }
        public void clear()
        {
            this.currentFrame = new STM1();
        }
    }
}
