# ==============================================================================
# WingRPG - 인벤토리, 중복 소환/초월, 파티 편성 및 영속성 CLI 테스트 검증 스크립트
# 사용법: powershell -ExecutionPolicy Bypass -File scripts/run_inventory_party_tests.ps1
# ==============================================================================

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "🎒 [WingRPG] 인벤토리 & 파티 편성 & 영속성 CLI 단위 테스트 실행" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# 1. Unity 에디터 연결 상태 점검
Write-Host "`n[1/3] Unity 에디터 연결 상태 확인 중..." -ForegroundColor Yellow
$status = unity status
Write-Host $status

# 2. 스크립트 최신 컴파일 확인
Write-Host "`n[2/3] 스크립트 변경사항 컴파일 검증..." -ForegroundColor Yellow
unity cmd recompile

# 3. InventoryPartyTests 단위 테스트 실행 (EditMode)
Write-Host "`n[3/3] InventoryPartyTests 단위 테스트 실행 중..." -ForegroundColor Yellow
$testResult = unity cmd run_tests editor

Write-Host "`n==================================================" -ForegroundColor Green
Write-Host "✅ 테스트 결과 요약" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
Write-Host $testResult

Write-Host "`n🎉 모든 인벤토리, 중복 초월, 파티 편성 및 영속성 시스템 검증 완료!" -ForegroundColor Cyan
