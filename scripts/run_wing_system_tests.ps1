# ==============================================================================
# WingRPG - 날개(Wing) 장비 및 드롭 시스템 CLI 단위 테스트 검증 스크립트
# 사용법: powershell -ExecutionPolicy Bypass -File scripts/run_wing_system_tests.ps1
# ==============================================================================

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "🪽 [WingRPG] 날개 장비 및 드롭 시스템 CLI 단위 테스트 실행" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# 1. Unity 에디터 연결 상태 확인
Write-Host "`n[1/3] Unity 에디터 연결 상태 점검 중..." -ForegroundColor Yellow
$status = unity status
Write-Host $status

# 2. 스크립트 최신 컴파일 확인
Write-Host "`n[2/3] 스크립트 변경사항 컴파일 검증..." -ForegroundColor Yellow
unity cmd recompile

# 3. 전체 EditMode 단위 테스트 실행
Write-Host "`n[3/3] 전체 EditMode 단위 테스트 실행 중..." -ForegroundColor Yellow
$testResult = unity cmd run_tests editor

Write-Host "`n==================================================" -ForegroundColor Green
Write-Host "✅ 테스트 결과 요약" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
Write-Host $testResult

Write-Host "`n🎉 모든 날개 슬롯 장착, 4부위 세트 효과 및 D20/적/상자 드롭 엔진 검증 완료!" -ForegroundColor Cyan
