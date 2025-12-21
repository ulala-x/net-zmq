[![English](https://img.shields.io/badge/lang:en-red.svg)](README.md) [![한국어](https://img.shields.io/badge/lang:한국어-blue.svg)](README.ko.md)

# 멀티파트 확장 샘플

이 샘플은 ZeroMQ에서 멀티파트 메시지를 송수신하기 위한 편리한 확장 메서드를 제공하는 `SocketExtensions` 클래스를 보여줍니다.

## 개요

멀티파트 메시지는 ZeroMQ의 기본 개념으로, 여러 프레임을 단일 원자적 단위로 전송할 수 있게 합니다. `SocketExtensions` 클래스는 `SendMore` 플래그 관리와 프레임 반복을 자동으로 처리하는 고수준 메서드를 제공하여 멀티파트 메시지 작업을 단순화합니다.

## 확장 메서드

### SendMultipart 오버로드

1. **SendMultipart(MultipartMessage)**
   - 완전한 `MultipartMessage` 컨테이너를 전송합니다
   - 마지막 프레임을 제외한 모든 프레임에 자동으로 `SendMore` 플래그를 적용합니다

2. **SendMultipart(IEnumerable<byte[]>)**
   - 바이트 배열 컬렉션을 멀티파트로 전송합니다
   - 바이너리 프로토콜에 유용합니다

3. **SendMultipart(params string[])**
   - 문자열 프레임을 멀티파트로 전송합니다
   - 각 문자열은 자동으로 UTF-8로 인코딩됩니다
   - 간단한 텍스트 기반 프로토콜에 가장 편리합니다

4. **SendMultipart(IEnumerable<Message>)**
   - `Message` 객체 컬렉션을 멀티파트로 전송합니다
   - 각 프레임에 대한 세밀한 제어를 제공합니다

### RecvMultipart 메서드

1. **RecvMultipart()**
   - 완전한 멀티파트 메시지의 블로킹 수신입니다
   - 모든 프레임을 포함하는 `MultipartMessage`를 반환합니다
   - `HasMore` 루프 처리를 간소화합니다

2. **TryRecvMultipart(out MultipartMessage?)**
   - 멀티파트 메시지의 논블로킹 수신입니다
   - 사용 가능한 메시지가 없으면 (블로킹될 경우) `false`를 반환합니다
   - 사용 가능한 메시지가 있으면 완전한 메시지와 함께 `true`를 반환합니다

## 이 샘플의 예제

### 예제 1: 문자열 파라미터 (가장 간단)
```csharp
sender.SendMultipart("Header", "Body", "Footer");
```

### 예제 2: 바이너리 프레임
```csharp
var frames = new List<byte[]>
{
    new byte[] { 0x01, 0x02, 0x03 },
    new byte[] { 0x04, 0x05 }
};
sender.SendMultipart(frames);
```

### 예제 3: MultipartMessage 컨테이너
```csharp
using var message = new MultipartMessage();
message.Add("Command");
message.AddEmptyFrame(); // 구분자
message.Add(binaryData);
sender.SendMultipart(message);
```

### 예제 4: 블로킹 수신
```csharp
using var received = receiver.RecvMultipart();
for (int i = 0; i < received.Count; i++)
{
    Console.WriteLine(received[i].ToString());
}
```

### 예제 5: 논블로킹 수신
```csharp
if (receiver.TryRecvMultipart(out var message))
{
    // 메시지 사용 가능
    using (message)
    {
        ProcessMessage(message);
    }
}
else
{
    // 블로킹될 상황, 사용 가능한 메시지 없음
}
```

### 예제 6: Router-Dealer 패턴
Router-Dealer 패턴에서 확장 메서드가 어떻게 엔벨로프 처리를 단순화하는지 보여줍니다.

## 확장 메서드의 장점

1. **간소화된 코드**: `SendMore` 플래그를 수동으로 추적할 필요가 없습니다
2. **오류 감소**: 마지막 프레임 처리를 자동화합니다 (`SendMore` 없음)
3. **깔끔한 API**: 루프 대신 단일 메서드 호출입니다
4. **가독성 향상**: `SendMultipart()`로 의도가 더 명확합니다
5. **리소스 안전성**: `RecvMultipart()`는 폐기 가능한 컨테이너를 반환합니다

## 기존 방식 vs 확장 메서드

### 확장 메서드 없이
```csharp
// 전송
sender.Send("Frame1", SendFlags.SendMore);
sender.Send("Frame2", SendFlags.SendMore);
sender.Send("Frame3", SendFlags.None); // SendMore 제거를 잊지 마세요!

// 수신
var frames = new List<byte[]>();
do
{
    frames.Add(receiver.RecvBytes());
} while (receiver.HasMore);
```

### 확장 메서드 사용
```csharp
// 전송
sender.SendMultipart("Frame1", "Frame2", "Frame3");

// 수신
using var message = receiver.RecvMultipart();
// 모든 프레임은 message[0], message[1], message[2]에 있습니다
```

## 샘플 실행

```bash
cd samples/NetZeroMQ.Samples.MultipartExtensions
dotnet run
```

## 핵심 개념

1. **멀티파트 원자성**: 멀티파트 메시지의 모든 프레임은 함께 전달되거나 전혀 전달되지 않습니다
2. **SendMore 플래그**: 현재 메시지에 더 많은 프레임이 뒤따른다는 것을 나타냅니다
3. **HasMore 속성**: 현재 프레임 뒤에 더 많은 부분이 있는지 나타냅니다
4. **빈 프레임**: 주로 구분자로 사용됩니다 (예: REQ/REP 엔벨로프 패턴)

## 일반적인 사용 사례

- **프로토콜 헤더**: 메타데이터와 페이로드를 분리합니다
- **라우팅 엔벨로프**: Router 소켓이 식별 프레임을 추가합니다
- **구조화된 메시지**: 명령 + 파라미터 구조입니다
- **바이너리 프로토콜**: 텍스트와 바이너리 데이터를 혼합합니다
- **메시지 구분**: 빈 프레임을 구분자로 사용합니다

## 메모리 관리

`RecvMultipart()` 또는 `TryRecvMultipart()`를 사용할 때는 항상 반환된 `MultipartMessage`를 폐기하세요:

```csharp
using var message = receiver.RecvMultipart();
// message 사용...
// 스코프 끝에서 자동으로 폐기됩니다
```

이렇게 하면 모든 기본 `Message` 객체가 적절히 해제됩니다.
