# Test Integrations — 통합 테스트 픽스처

개발용 모드로 `test_integrations__probe`의 조건부 로드를 검사합니다. `fixture: true`이므로 일반 게임과 `--all-mods`에서 제외되며 `--with-mods=test_integrations`로 명시적으로 추가합니다.

`probe`는 `quests_base`와 함께일 때만 로드됩니다. 이야기, 콘텐츠, 저장 데이터가 없습니다. 게임에서 존재, 프런트엔드에서 부재, 같은 프로세스 내 모드 전환을 검사합니다.

```text
dotnet run --project tests/Cthangover.TestRunner -- --test integrations --test integrations_frontend
```
