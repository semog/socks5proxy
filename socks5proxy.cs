/*
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SOCKS5
{
	public static class ProxySrv
    {
		public static void Main()
        {
            var s2 = new TcpListener(IPAddress.Loopback, 9998);
            s2.Start();

            while(true)
            {
                if(s2.Pending())
                {
                    Thread test = new Thread(() =>
                    {
                        using(TcpClient client = s2.AcceptTcpClient())
                        {
                            RunProxy(client);
                        }
                    })
                    {
                        IsBackground = true
                    };

                    test.Start();
                }

                Thread.Sleep(10);
            }
        }

        private static void RunProxy(TcpClient listener)
        {
            try
            {
                var ns1 = listener.GetStream();
                var r1 = new BinaryReader(ns1);
                var w1 = new BinaryWriter(ns1);

				if(!(r1.ReadByte() == 5 && r1.ReadByte() == 1))
				{
					return;
				}

                var c = r1.ReadByte();
				for(int i = 0; i < c; ++i)
				{
					r1.ReadByte();
				}

                w1.Write((byte) 5);
                w1.Write((byte) 0);

				if(!(r1.ReadByte() == 5 && r1.ReadByte() == 1))
				{
					return;
				}

				if(r1.ReadByte() != 0)
				{
					return;
				}

                byte[] ipAddr = null;
                string hostname = null;
                var type = r1.ReadByte();

                switch(type)
                {
                case 1:
                    ipAddr = r1.ReadBytes(4);
                    break;
                case 3:
                    hostname = Encoding.ASCII.GetString(r1.ReadBytes(r1.ReadByte()));
                    break;
                case 4:
                    throw new Exception();
                }

                var nhport = r1.ReadInt16();
                var port = IPAddress.NetworkToHostOrder(nhport);

                var socketout = new TcpClient();
				if(hostname != null)
				{
					socketout.Connect(hostname, port);
				}
				else
				{
					socketout.Connect(new IPAddress(ipAddr), port);
				}

                w1.Write((byte) 5);
                w1.Write((byte) 0);
                w1.Write((byte) 0);
                w1.Write(type);
                switch(type)
                {
                case 1:
                    w1.Write(ipAddr);
                    break;
                case 2:
                    w1.Write((byte) hostname.Length);
                    w1.Write(Encoding.ASCII.GetBytes(hostname), 0, hostname.Length);
                    break;
                }
                w1.Write(nhport);

                var buf1 = new byte[4096];
                var buf2 = new byte[4096];
                var ns2 = socketout.GetStream();
                DateTime last = DateTime.Now;

                while((DateTime.Now - last).TotalMinutes < 5.0)
                {
                    if(ns1.DataAvailable)
                    {
                        int size = ns1.Read(buf1, 0, buf1.Length);
                        ns2.Write(buf1, 0, size);
                        last = DateTime.Now;
                    }

                    if(ns2.DataAvailable)
                    {
                        int size = ns2.Read(buf2, 0, buf2.Length);
                        ns1.Write(buf2, 0, size);
                        last = DateTime.Now;
                    }

                    Thread.Sleep(10);
                }
            }
            catch
            {
                // Ignore errors.
            }
            finally
            {
                try
                {
                    listener.Close();
                }
                catch
                {
                    // Ignore errors.
                }
            }
        }
    }
}
