namespace Cthangover.Core.Mods.Host
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The transport-independent pipe between channels and the host core. Inbound side: transports enqueue
    /// request packets; the host core dequeues exactly one per admitted step, so the established
    /// one-command-at-a-time game discipline holds no matter how many channels or clients exist. Outbound
    /// side: the core publishes response packets addressed to one connection and debugging events published
    /// on a topic. A transport subscribes its event sink to the topics it can deliver, and topic packets are
    /// re-addressed to that sink, so transports never interpret payloads and the host never knows which
    /// channel a topic left through.
    /// <para>
    /// Ownership and threading: exactly one consumer (<see cref="DebugHost"/>) drains the inbound queue on
    /// the game thread, and <see cref="Deliver"/> also runs there — transports must therefore not block in
    /// the outbound handler (a slow write has to be queued by the transport itself). The bus holds no
    /// payload decoding, no retries and no history: a client that was not subscribed when an event was
    /// delivered missed it, which is why the error tail is also readable by pull
    /// (<c>errors.pull</c> over the command registry).
    /// </para>
    /// </summary>
    public sealed class DebugHostBus
    {
        private const int MaxOperations = 64;
        private static long connectionCounter;

        private readonly ConcurrentQueue<DebugHostPacket> inbound = new();
        private readonly object requestAdmission = new();
        private readonly Dictionary<Guid, DebugHostOperation> operations = new();
        private readonly LinkedList<Guid> operationOrder = new();
        private bool requestsClosed;

        private readonly Dictionary<string, List<long>> topicSubscriptions = new(StringComparer.Ordinal);

        private readonly HashSet<long> connections = new();

        /// <summary>Connections that have neither disconnected nor finished; the handshake log reports it.</summary>
        public int ActiveConnections
        {
            get
            {
                lock (connections)
                {
                    return connections.Count;
                }
            }
        }

        /// <summary>
        /// Allocates a process-unique connection number, marks the connection active and writes the opening
        /// trace line. Transports must pair it with <see cref="CloseConnection"/> in their teardown path.
        /// </summary>
        public long OpenConnection(string transport)
        {
            var id = Interlocked.Increment(ref connectionCounter);
            lock (connections)
            {
                connections.Add(id);
            }

            DebugHostInstance.Log("connection " + id + " opened via " + transport + " (active " + ActiveConnections + ")");
            return id;
        }

        /// <summary>Retires one connection; the closing trace line carries the remaining active count.</summary>
        public void CloseConnection(long id)
        {
            bool removed;
            lock (connections)
            {
                removed = connections.Remove(id);
            }

            if (removed)
            {
                DebugHostInstance.Log("connection " + id + " closed (active " + ActiveConnections + ")");
            }
        }

        /// <summary>Raised on the game thread for every outbound packet; transports filter by connection.</summary>
        public event Action<DebugHostPacket> Outbound;

        /// <summary>Enqueues one request packet from a transport; never blocks.</summary>
        public void Send(DebugHostPacket packet)
        {
            lock (requestAdmission)
            {
                if (!requestsClosed)
                    inbound.Enqueue(packet);
            }
        }

        /// <summary>
        /// Submits an owned snapshot to the common game-thread queue. The waiting caller can abandon it
        /// immediately even while the game thread is busy; a cancelled request is never dispatched later.
        /// An optional operation ID retains the result, and <c>operation.status</c> reads it without waiting
        /// for the game thread. A retry using the same ID cannot run the command twice.
        /// </summary>
        public Task<JsonObject> RequestAsync(JsonObject payload, long connection, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(payload);
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<JsonObject>(cancellationToken);

            var request = (JsonObject)payload.DeepClone();
            TaskCompletionSource<JsonObject> completion;
            DebugHostOperation operation = null;
            lock (requestAdmission)
            {
                if (requestsClosed)
                    return Task.FromResult(ClosedResponse());

                if (IsOperationStatus(request))
                    return Task.FromResult(OperationStatus(request));

                if (request["operation_id"] != null)
                {
                    if (!TryGetOperationId(request, out var id))
                        return Task.FromResult(Failure("invalid_operation_id", "operation_id must be a GUID."));

                    request["operation_id"] = id.ToString("D");
                    if (operations.TryGetValue(id, out var existing))
                    {
                        return existing.Matches(request)
                            ? existing.WaitAsync(cancellationToken)
                            : Task.FromResult(Failure("operation_conflict", "The operation ID already belongs to another request."));
                    }

                    if (!MakeRoomForOperation())
                        return Task.FromResult(Failure("operations_full", "The debug host has too many pending operations."));

                    operation = new DebugHostOperation(request);
                    operations.Add(id, operation);
                    operationOrder.AddLast(id);
                }

                completion = operation?.Completion ?? new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
                inbound.Enqueue(new DebugHostPacket(DebugHostPacketKind.Request, connection, string.Empty,
                    request, completion, cancellationToken, operation));
            }

            return operation != null ? operation.WaitAsync(cancellationToken) : completion.Task.WaitAsync(cancellationToken);
        }

        private static bool IsOperationStatus(JsonObject request)
        {
            return request["command"] is JsonValue value && value.TryGetValue<string>(out var command)
                && command == "operation.status";
        }

        private static bool TryGetOperationId(JsonObject request, out Guid id)
        {
            id = default;
            return request["operation_id"] is JsonValue value && value.TryGetValue<string>(out var text)
                && Guid.TryParse(text, out id);
        }

        private JsonObject OperationStatus(JsonObject request)
        {
            if (!TryGetOperationId(request, out var id))
                return Failure("invalid_operation_id", "operation_id must be a GUID.");

            return operations.TryGetValue(id, out var operation)
                ? operation.Status(id)
                : Failure("unknown_operation", "No result remains for this operation ID in this game instance.");
        }

        private bool MakeRoomForOperation()
        {
            if (operations.Count < MaxOperations)
                return true;

            for (var item = operationOrder.First; item != null; item = item.Next)
            {
                if (!operations[item.Value].Completion.Task.IsCompleted)
                    continue;

                operations.Remove(item.Value);
                operationOrder.Remove(item);
                return true;
            }

            return false;
        }

        internal void CloseRequests()
        {
            lock (requestAdmission)
            {
                requestsClosed = true;
                while (inbound.TryDequeue(out var packet))
                    packet.Completion?.TrySetResult(ClosedResponse());
            }
        }

        private static JsonObject ClosedResponse() => Failure("host_stopping", "The debug host is shutting down.");

        private static JsonObject Failure(string code, string error) => new()
        {
            ["ok"] = false,
            ["code"] = code,
            ["error"] = error
        };

        /// <summary>Dequeues the next request packet for the game-thread dispatch, if any.</summary>
        public bool TryReceive(out DebugHostPacket packet)
        {
            while (inbound.TryDequeue(out packet))
            {
                if (!packet.Cancellation.IsCancellationRequested)
                {
                    packet.Operation?.MarkStarted();
                    return true;
                }

                packet.Completion?.TrySetCanceled(packet.Cancellation);
            }

            return false;
        }

        /// <summary>
        /// Publishes an outbound packet. Connection-addressed packets go to every transport, which filters
        /// by its own connection numbers; topic packets are delivered only to transports that subscribed
        /// their event sink, re-addressed to that sink.
        /// </summary>
        public void Deliver(DebugHostPacket packet)
        {
            if (packet.Kind == DebugHostPacketKind.Event && packet.Connection == 0)
            {
                if (!topicSubscriptions.TryGetValue(packet.Topic ?? "", out var sinks))
                {
                    return;
                }

                foreach (var sink in sinks)
                {
                    Outbound?.Invoke(new DebugHostPacket(packet.Kind, sink, packet.Topic, packet.Payload));
                }

                return;
            }

            Outbound?.Invoke(packet);
        }

        /// <summary>
        /// Subscribes a transport's event sink (its connection number) to a topic; topic packets are
        /// re-addressed to the sink on delivery.
        /// </summary>
        public void Subscribe(string topic, long sink)
        {
            if (!topicSubscriptions.TryGetValue(topic, out var sinks))
            {
                topicSubscriptions.Add(topic, sinks = new List<long>());
            }

            if (!sinks.Contains(sink))
            {
                sinks.Add(sink);
            }
        }

        /// <summary>Removes every topic subscription of one transport sink (used when a transport stops).</summary>
        public void UnsubscribeAll(long sink)
        {
            foreach (var topic in topicSubscriptions.Keys)
            {
                topicSubscriptions[topic].Remove(sink);
            }
        }
    }
}
