[![English](https://img.shields.io/badge/lang-en-red.svg)](DEBUGGING_ANALYSIS.md) [![한국어](https://img.shields.io/badge/lang-ko-blue.svg)](DEBUGGING_ANALYSIS.ko.md)

# 성능 분석 아카이브

## 참고

이 파일은 이전에 MessagePool 구현에 대한 상세한 성능 분석을 포함하고 있었으나, Net.Zmq에서 더 단순한 4가지 전략 접근 방식으로 변경되면서 제거되었습니다:

1. **ByteArray** - 단순 관리 메모리 (managed memory) 할당
2. **ArrayPool** - 풀링된 관리 버퍼 (≤512B에 최적)
3. **Message** - 네이티브 libzmq 메시지 구조
4. **MessageZeroCopy** - 제로카피 (zero-copy)를 사용하는 비관리 메모리 (>512B에 최적)

MessagePool 기능이 제거된 이유:
- 대부분의 사용 사례에서 명확한 성능 이점 없이 복잡성만 증가
- ArrayPool과 MessageZeroCopy가 성능 스펙트럼을 효과적으로 커버
- 더 단순한 API 표면적으로 유지보수성 향상

현재 성능 권장사항은 다음을 참조하세요:
- [docs/benchmarks.ko.md](/docs/benchmarks.ko.md)
- [benchmarks/Net.Zmq.Benchmarks/README.ko.md](Net.Zmq.Benchmarks/README.ko.md)
