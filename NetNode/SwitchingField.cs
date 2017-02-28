﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClientWindow;
using Management;

namespace NetNode
{
    //switching field that have FIB and commutate frame from inport to outport
    class SwitchingField
    {
        public static List<FIB> fib = new List<FIB>();

        public int commutateContainer(VirtualContainer4 container, int iport)
        {
            int out_pos = -1;
            if (container != null)
            {
                //mamy do czynienia z vc4
                foreach (var row in fib)
                {
                    if (row.iport == iport) // && row.in_cont == 1)
                    {
                        out_pos = row.oport;
                        NetNode.log("Commutating vc4 container from:" + row.iport + " to " + row.oport, ConsoleColor.Green);
                        return out_pos;
                    }
                }
            }
            return out_pos;
        }
        public int[] commutateContainer(VirtualContainer3 container, int iport, int pos)
        {
            int[] out_pos = { -1, -1 };
            if (container != null)
            {
                //mamy do czynienia z vc3
                foreach (var row in fib)
                {
                    if (row.iport == iport && row.in_cont == pos)
                    {
                        out_pos[0] = row.oport;
                        out_pos[1] = row.out_cont;
                        NetNode.log("Commutating vc3 container from:" + row.iport + " " + row.in_cont + "to " + row.oport + " " + row.out_cont, ConsoleColor.Green);
                        return out_pos;
                    }
                }
            }
            return out_pos;
        }
        public static void addToSwitch(FIB row)
        {
            int counter = 0;
            if (fib.Count == 0)
            {
                fib.Add(row);
                Console.WriteLine("New fib entry added");
            }
            else
            {
                foreach (var fibrow in fib)
                {
                    if (fibrow.iport == row.iport && fibrow.in_cont == row.in_cont)// && fibrow.oport == row.oport && fibrow.out_cont == row.out_cont)
                    {
                        Console.WriteLine("Updating already stored");
                        fib.Remove(fibrow);
                        fib.Add(row);
                        break;
                    }
                    counter++;
                }
                if (counter == fib.Count)
                {
                    fib.Add(row);
                    Console.WriteLine("New fib entry added");
                }
            }          
        }

        public static void printFibTable()
        {
//            foreach (var temp in fib)
//            {
//                Console.WriteLine(temp.toString());
//            }
            for(int i=0;i<fib.Count;i+=2)
            {
                NetNode.log(fib[i].toString(), ConsoleColor.Cyan);
            }
        }

        public static void clearFibTable()
        {
            fib.Clear();
        }

        public static void clearFib(FIB row)
        {
            Console.WriteLine("Removing fib: " + row.toString());
            int id = 0;
            foreach (var temp in fib)
            {
                if (row.iport == temp.iport)
                    if (row.in_cont == temp.in_cont)
                        if (row.oport == temp.oport)
                            if (row.out_cont == temp.out_cont)
                                id = fib.IndexOf(temp);
            }
            fib.RemoveAt(id);
        }
    }
}