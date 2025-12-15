# NetZeroMQ XPub-XSub Proxy Pattern Sample

This sample demonstrates the ZeroMQ XPub-XSub proxy pattern using NetZeroMQ.

## Architecture

```
Publishers --> XSub (Frontend) --> Proxy --> XPub (Backend) --> Subscribers
```

## Components

### Proxy
- **Frontend (XSub)**: Receives messages from publishers on `tcp://*:5559`
- **Backend (XPub)**: Distributes messages to subscribers on `tcp://*:5560`
- Forwards messages from frontend to backend
- Forwards subscriptions from backend to frontend

### Publishers
- **Publisher-1**: Publishes "weather" topic messages
- **Publisher-2**: Publishes "sports" topic messages
- Both connect to proxy's XSub socket at `tcp://localhost:5559`

### Subscribers
- **Subscriber-1**: Subscribes to "weather" topic
- **Subscriber-2**: Subscribes to "sports" topic
- **Subscriber-3**: Subscribes to both "weather" and "sports" topics
- All connect to proxy's XPub socket at `tcp://localhost:5560`

## Key Features Demonstrated

1. **Topic-Based Filtering**: Subscribers receive only messages matching their subscriptions
2. **Dynamic Subscriptions**: Subscriptions are handled at runtime
3. **Multiple Publishers**: Multiple publishers can send to the same proxy
4. **Multiple Subscribers**: Multiple subscribers can receive from the same proxy
5. **Built-in Proxy**: Uses `Proxy.Start()` utility for automatic message forwarding

## Running the Sample

```bash
cd samples/NetZeroMQ.Samples.Proxy
dotnet run
```

## Expected Output

The sample will:
1. Start the XPub-XSub proxy
2. Start two publishers publishing different topics
3. Start three subscribers with different topic subscriptions
4. Display message flow showing which subscribers receive which messages
5. Demonstrate unsubscription at the end

## ZeroMQ Proxy Pattern Benefits

- **Decoupling**: Publishers and subscribers don't need to know about each other
- **Scalability**: Easy to add more publishers or subscribers
- **Centralization**: Single point for monitoring and management
- **Flexibility**: Can add filtering, logging, or capture sockets

## Technical Details

### Socket Types
- **XSub**: Extended subscriber socket (used on proxy frontend)
- **XPub**: Extended publisher socket (used on proxy backend)

### Why XPub-XSub?
- Standard PUB-SUB sockets can't be used in proxy pattern
- XPUB forwards subscription messages upstream
- XSUB accepts subscription messages from downstream
- This allows dynamic topic filtering through the proxy

## Code Structure

- **RunProxy()**: Creates XSub and XPub sockets, starts built-in proxy
- **RunPublisher()**: Connects to proxy frontend, publishes topic messages
- **RunSubscriber()**: Connects to proxy backend, subscribes to topics, receives messages
