using System;
using System.Collections.Generic;

using BestHTTP.Connections;
using BestHTTP.Extensions;

namespace BestHTTP.Core
{
    public enum HostProtocolSupport : byte
    {
        Unknown = 0x00,
        HTTP1   = 0x01,
        HTTP2   = 0x02
    }

    /// <summary>
    /// A HostConnection object manages the connections to a host and the request queue.
    /// </summary>
    public sealed class HostConnection
    {
        public HostDefinition Host { get; private set; }

        public string VariantId { get; private set; }

        public HostProtocolSupport ProtocolSupport { get; private set; }
        public DateTime LastProtocolSupportUpdate { get; private set; }
        
        public int QueuedRequests { get { return this.Queue.Count; } }

        private List<ConnectionBase> Connections = new List<ConnectionBase>();
        private List<HTTPRequest> Queue = new List<HTTPRequest>();

        public HostConnection(HostDefinition host, string variantId)
        {
            this.Host = host;
            this.VariantId = variantId;
        }

        internal void AddProtocol(HostProtocolSupport protocolSupport)
        {
            this.LastProtocolSupportUpdate = DateTime.UtcNow;

            var oldProtocol = this.ProtocolSupport;

            if (oldProtocol != protocolSupport)
            {
                this.ProtocolSupport = protocolSupport;

                HTTPManager.Logger.Information(typeof(HostConnection).Name, string.Format("'{0}': AddProtocol({1}) - changing from {2} to {3}", this.VariantId, protocolSupport, oldProtocol, this.ProtocolSupport));

                HostManager.Save();

                TryToSendQueuedRequests();
            }
        }

        internal HostConnection Send(HTTPRequest request)
        {
            var conn = GetNextAvailable(request);

            if (conn != null)
            {
                request.State = HTTPRequestStates.Processing;

                request.Prepare();

                // then start process the request
                conn.Process(request);
            }
            else
            {
                // If no free connection found and creation prohibited, we will put back to the queue
                this.Queue.Add(request);
            }

            return this;
        }

        internal ConnectionBase GetNextAvailable(HTTPRequest request)
        {
            int activeConnections = 0;
            ConnectionBase conn = null;
            // Check the last created connection first. This way, if a higher level protocol is present that can handle more requests (== HTTP/2) that protocol will be choosen
            //  and others will be closed when their inactivity time is reached.
            for (int i = Connections.Count - 1; i >= 0; --i)
            {
                conn = Connections[i];

                if (conn.State == HTTPConnectionStates.Initial || conn.State == HTTPConnectionStates.Free || conn.CanProcessMultiple)
                {
                    HTTPManager.Logger.Verbose("HostConnection", string.Format("'{0}': GetNextAvailable - returning with connection. state: {1}, CanProcessMultiple: {2}", this.VariantId, conn.State, conn.CanProcessMultiple));
                    return conn;
                }

                activeConnections++;
            }

            if (activeConnections >= HTTPManager.MaxConnectionPerServer)
            {
                HTTPManager.Logger.Verbose("HostConnection", string.Format("'{0}': GetNextAvailable - activeConnections({1}) >= HTTPManager.MaxConnectionPerServer({2})", this.VariantId, activeConnections, HTTPManager.MaxConnectionPerServer));
                return null;
            }

            string key = HostDefinition.GetKeyForRequest(request);

            conn = null;

#if UNITY_WEBGL && !UNITY_EDITOR
            conn = new WebGLConnection(key);
#else
            if (request.CurrentUri.IsFile)
                conn = new FileConnection(key);
            else
            {
#if !BESTHTTP_DISABLE_ALTERNATE_SSL
                // Hold back the creation of a new connection until we know more about the remote host's features.
                // If we send out multiple requests at once it will execute the first and delay the others. 
                // While it will decrease performance initially, it will prevent the creation of TCP connections
                //  that will be unused after their first request processing if the server supports HTTP/2.
                if (activeConnections >= 1 && (this.ProtocolSupport == HostProtocolSupport.Unknown || this.ProtocolSupport == HostProtocolSupport.HTTP2))
                {
                    HTTPManager.Logger.Verbose("HostConnection", string.Format("'{0}': GetNextAvailable - waiting for protocol support message. activeConnections: {1}, ProtocolSupport: {2} ", this.VariantId, activeConnections, this.ProtocolSupport));
                    return null;
                }
#endif

                HTTPManager.Logger.Verbose("HostConnection", string.Format("'{0}': GetNextAvailable - creating new connection, key: {1} ", this.VariantId, key));
                conn = new HTTPConnection(key);
            }
#endif

                Connections.Add(conn);

            return conn;
        }

        internal HostConnection RecycleConnection(ConnectionBase conn)
        {
            conn.State = HTTPConnectionStates.Free;

            BestHTTP.Extensions.Timer.Add(new TimerData(TimeSpan.FromSeconds(1), conn, CloseConnectionAfterInactivity));

            return this;
        }

        internal HostConnection RemoveConnection(ConnectionBase conn, HTTPConnectionStates setState)
        {
            conn.State = setState;
            conn.Dispose();

            bool found = this.Connections.Remove(conn);

            if (!found)
                HTTPManager.Logger.Warning(typeof(HostConnection).Name, string.Format("'{0}': RemoveConnection - Couldn't find connection! key: {1}", this.VariantId, conn.ServerAddress));

            return this;
        }

        internal HostConnection TryToSendQueuedRequests()
        {
            while (this.Queue.Count > 0 && GetNextAvailable(this.Queue[0]) != null)
            {
                Send(this.Queue[0]);
                this.Queue.RemoveAt(0);
            }

            return this;
        }

        private bool CloseConnectionAfterInactivity(DateTime now, object context)
        {
            var conn = context as ConnectionBase;

            bool closeConnection = conn.State == HTTPConnectionStates.Free && now - conn.LastProcessTime >= conn.KeepAliveTime;
            if (closeConnection)
            {
                HTTPManager.Logger.Information(typeof(HostConnection).Name, string.Format("'{0}': CloseConnectionAfterInactivity - [{1}] Closing! State: {2}, Now: {3}, LastProcessTime: {4}, KeepAliveTime: {5}",
                    this.VariantId, conn.ToString(), conn.State, now, conn.LastProcessTime, conn.KeepAliveTime));

                RemoveConnection(conn, HTTPConnectionStates.Closed);
                return false;
            }

            // repeat until the connection's state is free
            return conn.State == HTTPConnectionStates.Free;
        }

        internal void Shutdown()
        {
            this.Queue.Clear();

            foreach (var conn in this.Connections)
            {
                // Swallow any exceptions, we are quitting anyway.
                try
                {
                    conn.Shutdown(ShutdownTypes.Immediate);
                }
                catch { }
            }
            //this.Connections.Clear();
        }

        internal void SaveTo(System.IO.BinaryWriter bw)
        {
            bw.Write(this.LastProtocolSupportUpdate.ToBinary());
            bw.Write((byte)this.ProtocolSupport);
        }

        internal void LoadFrom(int version, System.IO.BinaryReader br)
        {
            this.LastProtocolSupportUpdate = DateTime.FromBinary(br.ReadInt64());
            this.ProtocolSupport = (HostProtocolSupport)br.ReadByte();

            if (DateTime.UtcNow - this.LastProtocolSupportUpdate >= TimeSpan.FromDays(1))
            {
                HTTPManager.Logger.Verbose("HostConnection", string.Format("'{0}': LoadFrom - Too Old! LastProtocolSupportUpdate: {1}, ProtocolSupport: {2}", this.VariantId, this.LastProtocolSupportUpdate, this.ProtocolSupport));
                this.ProtocolSupport = HostProtocolSupport.Unknown;                
            }
            else
                HTTPManager.Logger.Verbose("HostConnection", string.Format("'{0}': LoadFrom - LastProtocolSupportUpdate: {1}, ProtocolSupport: {2}", this.VariantId, this.LastProtocolSupportUpdate, this.ProtocolSupport));
        }
    }
}
