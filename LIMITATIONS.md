# Limitations

이 프로그램은 PSA 공식 심사 결과를 대체하지 않습니다. UI에 표시되는 문구와 같이, 결과는 이미지 분석을 기반으로 한 **예상 등급**입니다.

## 예측의 한계

- 이미지만으로 PSA 실제 grade를 완벽하게 예측할 수 없습니다.
- PSA grader의 subjective eye appeal를 완전히 재현할 수 없습니다.
- 공개된 grading guideline과 측정값을 규칙으로 적용할 뿐, PSA 내부 알고리즘을 복제하지 않습니다.
- foil/holo reflection이 surface 분석에 영향을 줄 수 있습니다. 밝은 픽셀을 무조건 결함으로 보지 않지만, 강한 반사는 분석 신뢰도를 낮춥니다.
- 미세 dent는 일반 정면 사진에서 검출하기 어렵습니다. 사광 사진은 Surface 분석에서 사용됩니다.
- sleeve / toploader 상태에서는 분석 정확도가 감소합니다.
- 해상도 부족, 심한 blur, 카드 일부 잘림, 과도한 원근 왜곡이 있으면 등급을 강제 산출하지 않습니다.

## 판정 불가 영역

- alteration / authenticity (trimming, recoloring, cleaning, restoration)는 이미지만으로 확정 판정하지 않습니다.
- PSA 적중 확률(%)은 표시하지 않습니다. 분석 신뢰도는 입력 사진의 분석 가능 정도입니다.

## 데이터

향후 실제 PSA 등급 피드백을 저장할 수 있지만, 충분한 검증 데이터가 쌓이기 전까지 통계적 확률 모델로 해석하면 안 됩니다.
