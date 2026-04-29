# Addressables 기반 에셋 로딩 & 관리 시스템

## 기능 개요
Addressables의 번거로운 로드/해제 과정을 단순화하고 메모리 누수를 방지하는 구조를 설계했으며, 버전 기반 콘텐츠 관리까지 확장한 시스템

## 핵심 흐름
1. AddressableAutoLabeler를 통해 에셋에 자동으로 Label 및 Address를 부여
2. AddressableAssetLoader를 통해 에셋을 로드하고, Key 기반으로 참조 관리
3. 에셋 사용 종료 시, 등록된 Key를 통해 안전하게 Release 처리

## 주요 코드
AddressableAutoLabeler.cs → 자동으로 Addressable 에셋들 라벨링 및 address 등록

AddressableAssetLoader.cs → Addressable 에셋 관리
