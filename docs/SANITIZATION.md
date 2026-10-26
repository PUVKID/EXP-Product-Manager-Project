# 공개 사본의 연락처 비식별화

`Brand.cs` 12개의 연락 이메일 리터럴만 `CONTACT_EMAIL` 환경변수에서 읽는 같은 이름 `Email` 속성으로 바꿨습니다. 컴파일 상수는 실행 시 환경변수를 읽을 수 없으므로 이 필드만 속성으로 전환했습니다. 저작자·법적 귀속·제품·버전·다른 로직은 수정하지 않았습니다.

설정값이 없으면 `InvalidOperationException`으로 필요한 변수명을 알립니다. 본인의 공개 연락처를 프로세스 환경에 설정해야 하며 개인 연락처를 소스에 다시 넣지 마세요. 환경변수 읽기는 [Microsoft의 공식 API 문서](https://learn.microsoft.com/dotnet/api/system.environment.getenvironmentvariable)를 따릅니다.

실제 개인키 XML 14개 제외는 이전 사본과 동일하게 유지했습니다. 키를 요구하는 기존 발급 도구에는 별도 보관한 개인키가 필요합니다. 새 라이선스나 키를 발급하지 않았습니다.
