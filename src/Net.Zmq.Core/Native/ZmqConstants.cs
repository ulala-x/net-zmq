namespace Net.Zmq.Core.Native;

/// <summary>
/// ZeroMQ constants for socket types, options, and error codes.
/// </summary>
internal static class ZmqConstants
{
    // Socket Types
    public const int ZMQ_PAIR = 0;
    public const int ZMQ_PUB = 1;
    public const int ZMQ_SUB = 2;
    public const int ZMQ_REQ = 3;
    public const int ZMQ_REP = 4;
    public const int ZMQ_DEALER = 5;
    public const int ZMQ_ROUTER = 6;
    public const int ZMQ_PULL = 7;
    public const int ZMQ_PUSH = 8;
    public const int ZMQ_XPUB = 9;
    public const int ZMQ_XSUB = 10;
    public const int ZMQ_STREAM = 11;

    // Context Options
    public const int ZMQ_IO_THREADS = 1;
    public const int ZMQ_MAX_SOCKETS = 2;
    public const int ZMQ_SOCKET_LIMIT = 3;
    public const int ZMQ_THREAD_PRIORITY = 3;
    public const int ZMQ_THREAD_SCHED_POLICY = 4;
    public const int ZMQ_MAX_MSGSZ = 5;
    public const int ZMQ_MSG_T_SIZE = 6;
    public const int ZMQ_THREAD_AFFINITY_CPU_ADD = 7;
    public const int ZMQ_THREAD_AFFINITY_CPU_REMOVE = 8;
    public const int ZMQ_THREAD_NAME_PREFIX = 9;

    // Socket Options
    public const int ZMQ_AFFINITY = 4;
    public const int ZMQ_ROUTING_ID = 5;
    public const int ZMQ_SUBSCRIBE = 6;
    public const int ZMQ_UNSUBSCRIBE = 7;
    public const int ZMQ_RATE = 8;
    public const int ZMQ_RECOVERY_IVL = 9;
    public const int ZMQ_SNDBUF = 11;
    public const int ZMQ_RCVBUF = 12;
    public const int ZMQ_RCVMORE = 13;
    public const int ZMQ_FD = 14;
    public const int ZMQ_EVENTS = 15;
    public const int ZMQ_TYPE = 16;
    public const int ZMQ_LINGER = 17;
    public const int ZMQ_RECONNECT_IVL = 18;
    public const int ZMQ_BACKLOG = 19;
    public const int ZMQ_RECONNECT_IVL_MAX = 21;
    public const int ZMQ_MAXMSGSIZE = 22;
    public const int ZMQ_SNDHWM = 23;
    public const int ZMQ_RCVHWM = 24;
    public const int ZMQ_MULTICAST_HOPS = 25;
    public const int ZMQ_RCVTIMEO = 27;
    public const int ZMQ_SNDTIMEO = 28;
    public const int ZMQ_LAST_ENDPOINT = 32;
    public const int ZMQ_ROUTER_MANDATORY = 33;
    public const int ZMQ_TCP_KEEPALIVE = 34;
    public const int ZMQ_TCP_KEEPALIVE_CNT = 35;
    public const int ZMQ_TCP_KEEPALIVE_IDLE = 36;
    public const int ZMQ_TCP_KEEPALIVE_INTVL = 37;
    public const int ZMQ_IMMEDIATE = 39;
    public const int ZMQ_XPUB_VERBOSE = 40;
    public const int ZMQ_ROUTER_RAW = 41;
    public const int ZMQ_IPV6 = 42;
    public const int ZMQ_MECHANISM = 43;
    public const int ZMQ_PLAIN_SERVER = 44;
    public const int ZMQ_PLAIN_USERNAME = 45;
    public const int ZMQ_PLAIN_PASSWORD = 46;
    public const int ZMQ_CURVE_SERVER = 47;
    public const int ZMQ_CURVE_PUBLICKEY = 48;
    public const int ZMQ_CURVE_SECRETKEY = 49;
    public const int ZMQ_CURVE_SERVERKEY = 50;
    public const int ZMQ_PROBE_ROUTER = 51;
    public const int ZMQ_REQ_CORRELATE = 52;
    public const int ZMQ_REQ_RELAXED = 53;
    public const int ZMQ_CONFLATE = 54;
    public const int ZMQ_ZAP_DOMAIN = 55;
    public const int ZMQ_ROUTER_HANDOVER = 56;
    public const int ZMQ_TOS = 57;
    public const int ZMQ_CONNECT_ROUTING_ID = 61;
    public const int ZMQ_GSSAPI_SERVER = 62;
    public const int ZMQ_GSSAPI_PRINCIPAL = 63;
    public const int ZMQ_GSSAPI_SERVICE_PRINCIPAL = 64;
    public const int ZMQ_GSSAPI_PLAINTEXT = 65;
    public const int ZMQ_HANDSHAKE_IVL = 66;
    public const int ZMQ_SOCKS_PROXY = 68;
    public const int ZMQ_XPUB_NODROP = 69;
    public const int ZMQ_BLOCKY = 70;
    public const int ZMQ_XPUB_MANUAL = 71;
    public const int ZMQ_XPUB_WELCOME_MSG = 72;
    public const int ZMQ_STREAM_NOTIFY = 73;
    public const int ZMQ_INVERT_MATCHING = 74;
    public const int ZMQ_HEARTBEAT_IVL = 75;
    public const int ZMQ_HEARTBEAT_TTL = 76;
    public const int ZMQ_HEARTBEAT_TIMEOUT = 77;
    public const int ZMQ_XPUB_VERBOSER = 78;
    public const int ZMQ_CONNECT_TIMEOUT = 79;
    public const int ZMQ_TCP_MAXRT = 80;
    public const int ZMQ_THREAD_SAFE = 81;
    public const int ZMQ_MULTICAST_MAXTPDU = 84;
    public const int ZMQ_VMCI_BUFFER_SIZE = 85;
    public const int ZMQ_VMCI_BUFFER_MIN_SIZE = 86;
    public const int ZMQ_VMCI_BUFFER_MAX_SIZE = 87;
    public const int ZMQ_VMCI_CONNECT_TIMEOUT = 88;
    public const int ZMQ_USE_FD = 89;
    public const int ZMQ_GSSAPI_PRINCIPAL_NAMETYPE = 90;
    public const int ZMQ_GSSAPI_SERVICE_PRINCIPAL_NAMETYPE = 91;
    public const int ZMQ_BINDTODEVICE = 92;

    // Send/Recv Flags
    public const int ZMQ_DONTWAIT = 1;
    public const int ZMQ_SNDMORE = 2;

    // Poll Events
    public const int ZMQ_POLLIN = 1;
    public const int ZMQ_POLLOUT = 2;
    public const int ZMQ_POLLERR = 4;
    public const int ZMQ_POLLPRI = 8;

    // Message Properties
    public const int ZMQ_MORE = 1;
    public const int ZMQ_SHARED = 3;

    // Security Mechanisms
    public const int ZMQ_NULL = 0;
    public const int ZMQ_PLAIN = 1;
    public const int ZMQ_CURVE = 2;
    public const int ZMQ_GSSAPI = 3;

    // Error Codes
    // EAGAIN is platform-specific in the OS, but libzmq normalizes
    // Windows Winsock errors to POSIX errno values via wsa_error_to_errno.
    // However, macOS still uses native errno (35 instead of 11).
    // - Linux: 11
    // - macOS: 35
    // - Windows: 11 (libzmq converts WSAEWOULDBLOCK to EAGAIN=11)
    public static readonly int EAGAIN = OperatingSystem.IsMacOS() ? 35 : 11;
    public const int ENOTSUP = 95;
    public const int EPROTONOSUPPORT = 93;
    public const int ENOBUFS = 105;
    public const int ENETDOWN = 100;
    public const int EADDRINUSE = 98;
    public const int EADDRNOTAVAIL = 99;
    public const int ECONNREFUSED = 111;
    public const int EINPROGRESS = 115;
    public const int ENOTSOCK = 88;
    public const int EMSGSIZE = 90;
    public const int EAFNOSUPPORT = 97;
    public const int ENETUNREACH = 101;
    public const int ECONNABORTED = 103;
    public const int ECONNRESET = 104;
    public const int ENOTCONN = 107;
    public const int ETIMEDOUT = 110;
    public const int EHOSTUNREACH = 113;
    public const int ENETRESET = 102;

    // ZMQ-specific error codes (base value)
    public const int ZMQ_HAUSNUMERO = 156384712;

    // ZMQ error codes
    public const int ETERM = ZMQ_HAUSNUMERO + 53;
    public const int ENOENT = 2;
    public const int EINTR = 4;
    public const int EACCES = 13;
    public const int EFAULT = 14;
    public const int EINVAL = 22;
    public const int EMFILE = 24;

    // Socket Monitor Events
    public const int ZMQ_EVENT_CONNECTED = 1;
    public const int ZMQ_EVENT_CONNECT_DELAYED = 2;
    public const int ZMQ_EVENT_CONNECT_RETRIED = 4;
    public const int ZMQ_EVENT_LISTENING = 8;
    public const int ZMQ_EVENT_BIND_FAILED = 16;
    public const int ZMQ_EVENT_ACCEPTED = 32;
    public const int ZMQ_EVENT_ACCEPT_FAILED = 64;
    public const int ZMQ_EVENT_CLOSED = 128;
    public const int ZMQ_EVENT_CLOSE_FAILED = 256;
    public const int ZMQ_EVENT_DISCONNECTED = 512;
    public const int ZMQ_EVENT_MONITOR_STOPPED = 1024;
    public const int ZMQ_EVENT_ALL = 0xFFFF;

    // Protocol Errors
    public const int ZMQ_PROTOCOL_ERROR_ZMTP_UNSPECIFIED = 0x10000000;
    public const int ZMQ_PROTOCOL_ERROR_ZMTP_UNEXPECTED_COMMAND = 0x10000001;
    public const int ZMQ_PROTOCOL_ERROR_ZMTP_INVALID_SEQUENCE = 0x10000002;
    public const int ZMQ_PROTOCOL_ERROR_ZMTP_KEY_EXCHANGE = 0x10000003;
    public const int ZMQ_PROTOCOL_ERROR_ZMTP_MALFORMED_COMMAND_UNSPECIFIED = 0x10000011;
    public const int ZMQ_PROTOCOL_ERROR_ZMTP_MALFORMED_COMMAND_MESSAGE = 0x10000012;
    public const int ZMQ_PROTOCOL_ERROR_ZMTP_MALFORMED_COMMAND_HELLO = 0x10000013;
    public const int ZMQ_PROTOCOL_ERROR_ZMTP_MALFORMED_COMMAND_INITIATE = 0x10000014;
    public const int ZMQ_PROTOCOL_ERROR_ZMTP_MALFORMED_COMMAND_ERROR = 0x10000015;
    public const int ZMQ_PROTOCOL_ERROR_ZMTP_MALFORMED_COMMAND_READY = 0x10000016;
    public const int ZMQ_PROTOCOL_ERROR_ZMTP_MALFORMED_COMMAND_WELCOME = 0x10000017;
    public const int ZMQ_PROTOCOL_ERROR_ZMTP_INVALID_METADATA = 0x10000018;
    public const int ZMQ_PROTOCOL_ERROR_ZMTP_CRYPTOGRAPHIC = 0x11000001;
    public const int ZMQ_PROTOCOL_ERROR_ZMTP_MECHANISM_MISMATCH = 0x11000002;
    public const int ZMQ_PROTOCOL_ERROR_ZAP_UNSPECIFIED = 0x20000000;
    public const int ZMQ_PROTOCOL_ERROR_ZAP_MALFORMED_REPLY = 0x20000001;
    public const int ZMQ_PROTOCOL_ERROR_ZAP_BAD_REQUEST_ID = 0x20000002;
    public const int ZMQ_PROTOCOL_ERROR_ZAP_BAD_VERSION = 0x20000003;
    public const int ZMQ_PROTOCOL_ERROR_ZAP_INVALID_STATUS_CODE = 0x20000004;
    public const int ZMQ_PROTOCOL_ERROR_ZAP_INVALID_METADATA = 0x20000005;
    public const int ZMQ_PROTOCOL_ERROR_WS_UNSPECIFIED = 0x30000000;
}
