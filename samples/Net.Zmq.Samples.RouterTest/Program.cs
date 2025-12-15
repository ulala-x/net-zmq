using System.Text;
using Net.Zmq;

Console.WriteLine("=== OneWayRouting 대용량 테스트 ===\n");

using var context = new Context();
using var router1 = new Socket(context, SocketType.Router);
using var router2 = new Socket(context, SocketType.Router);

var router1Id = Encoding.UTF8.GetBytes("router1");
var router2Id = Encoding.UTF8.GetBytes("router2");

router1.SetOption(SocketOption.Routing_Id, router1Id);
router2.SetOption(SocketOption.Routing_Id, router2Id);

router1.Bind("inproc://router-bench");
router2.Connect("inproc://router-bench");
Thread.Sleep(10);

var message = new byte[64];
Random.Shared.NextBytes(message);

int count = 10000;
Console.WriteLine($"OneWayRouting {count:N0}회 테스트 시작...");
var sw = System.Diagnostics.Stopwatch.StartNew();

for (int i = 0; i < count; i++)
{
    router1.Send(router2Id, SendFlags.SendMore);
    router1.Send(message);

    var identityMsg = new Message();
    router2.Recv(identityMsg);
    
    while (router2.HasMore)
    {
        var frameMsg = new Message();
        router2.Recv(frameMsg);
        frameMsg.Dispose();
    }
    identityMsg.Dispose();
    
    if ((i + 1) % 10000 == 0)
        Console.WriteLine($"  진행: {i + 1:N0}/{count:N0}");
}

sw.Stop();
Console.WriteLine($"\n완료! 총 시간: {sw.ElapsedMilliseconds}ms, 평균: {sw.Elapsed.TotalMicroseconds / count:F3}us/op");
