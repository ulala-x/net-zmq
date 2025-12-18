#!/bin/bash

# NetZMQ 벤치마크 실행 스크립트
# 사용법:
#   ./run-benchmarks.sh           # 전체 벤치마크 실행
#   ./run-benchmarks.sh memory    # MemoryStrategyBenchmarks만 실행
#   ./run-benchmarks.sh receive   # ReceiveModeBenchmarks만 실행

set -e  # 에러 발생 시 중단

# 스크립트 위치 감지 및 프로젝트 루트로 이동
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$PROJECT_ROOT"

BENCHMARK_PROJECT="benchmarks/Net.Zmq.Benchmarks"

# 색상 정의
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

print_header() {
    echo -e "${BLUE}============================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}============================================${NC}"
}

print_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

# 인자 파싱
MODE=${1:-all}

case $MODE in
    memory)
        print_header "Memory Strategy Benchmarks 실행"
        FILTER="*MemoryStrategyBenchmarks*"
        ;;
    receive)
        print_header "Receive Mode Benchmarks 실행"
        FILTER="*ReceiveModeBenchmarks*"
        ;;
    all|"")
        print_header "전체 Benchmarks 실행"
        FILTER=""
        ;;
    *)
        echo "사용법: $0 [memory|receive|all]"
        echo ""
        echo "옵션:"
        echo "  memory   - MemoryStrategyBenchmarks만 실행"
        echo "  receive  - ReceiveModeBenchmarks만 실행"
        echo "  all      - 전체 벤치마크 실행 (기본값)"
        exit 1
        ;;
esac

# 벤치마크 프로젝트 경로 확인
if [ ! -d "$BENCHMARK_PROJECT" ]; then
    print_warning "벤치마크 프로젝트를 찾을 수 없습니다: $BENCHMARK_PROJECT"
    exit 1
fi

# 빌드
print_info "Release 모드로 빌드 중..."
dotnet build -c Release

# 벤치마크 실행
print_info "벤치마크 실행 중..."
echo ""

if [ -z "$FILTER" ]; then
    # 전체 실행
    dotnet run -c Release --project "$BENCHMARK_PROJECT" --no-build
else
    # 필터 적용
    dotnet run -c Release --project "$BENCHMARK_PROJECT" --no-build -- --filter "$FILTER"
fi

# 완료
echo ""
print_header "벤치마크 완료!"
print_info "결과는 BenchmarkDotNet.Artifacts 디렉토리에 저장되었습니다."
